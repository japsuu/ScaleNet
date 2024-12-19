using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

public interface IBufferOwner
{
    void Return(ArrayBuffer buffer);
}

public sealed class ArrayBuffer : IDisposable
{
    private readonly IBufferOwner _owner;

    public readonly byte[] Array;

    /// <summary>
    /// number of bytes written to buffer
    /// </summary>
    public int Count { get; internal set; }


    /// <summary>
    /// How many times release needs to be called before buffer is returned to pool
    /// <para>This allows the buffer to be used in multiple places at the same time</para>
    /// </summary>
    public void SetReleasesRequired(int required)
    {
        _releasesRequired = required;
    }


    /// <summary>
    /// How many times release needs to be called before buffer is returned to pool
    /// <para>This allows the buffer to be used in multiple places at the same time</para>
    /// </summary>
    /// <remarks>
    /// This value is normally 0, but can be changed to require release to be called multiple times
    /// </remarks>
    private int _releasesRequired;


    public ArrayBuffer(IBufferOwner owner, int size)
    {
        _owner = owner;
        Array = new byte[size];
    }


    public void Release()
    {
        int newValue = Interlocked.Decrement(ref _releasesRequired);
        if (newValue <= 0)
        {
            Count = 0;
            _owner.Return(this);
        }
    }


    public void Dispose()
    {
        Release();
    }


    public void CopyTo(byte[] target, int offset)
    {
        if (Count > target.Length + offset)
            throw new ArgumentException($"{nameof(Count)} was greater than {nameof(target)}.length", nameof(target));

        Buffer.BlockCopy(Array, 0, target, offset, Count);
    }


    public void CopyFrom(byte[] source, int offset, int length)
    {
        if (length > Array.Length)
            throw new ArgumentException($"{nameof(length)} was greater than {nameof(Array)}.length", nameof(length));

        Count = length;
        Buffer.BlockCopy(source, offset, Array, 0, length);
    }


    public void CopyFrom(IntPtr bufferPtr, int length)
    {
        if (length > Array.Length)
            throw new ArgumentException($"{nameof(length)} was greater than {nameof(Array)}.length", nameof(length));

        Count = length;
        Marshal.Copy(bufferPtr, Array, 0, length);
    }


    public void CopyFrom(ref ReadOnlySpan<byte> source)
    {
        Count = source.Length;

        // May throw, or may not ;)
        source.CopyTo(Array);
    }


    public void CopyFrom(ref ReadOnlyMemory<byte> source)
    {
        Count = source.Length;

        // May throw, or may not ;)
        source.Span.CopyTo(Array);
    }


    public ArraySegment<byte> ToSegment() => new(Array, 0, Count);


    [Conditional("UNITY_ASSERTIONS")]
    internal void Validate(int arraySize)
    {
        if (Array.Length != arraySize)
            SimpleWebLog.Error("Buffer that was returned had an array of the wrong size");
    }
}

internal class BufferBucket : IBufferOwner
{
    public readonly int ArraySize;
    private readonly ConcurrentQueue<ArrayBuffer> _buffers;

    
    public BufferBucket(int arraySize)
    {
        ArraySize = arraySize;
        _buffers = new ConcurrentQueue<ArrayBuffer>();
    }


    public ArrayBuffer Take()
    {
        if (_buffers.TryDequeue(out ArrayBuffer? buffer))
            return buffer;

        return new ArrayBuffer(this, ArraySize);
    }


    public void Return(ArrayBuffer buffer)
    {
        _buffers.Enqueue(buffer);
    }
}

/// <summary>
/// Collection of different sized buffers
/// </summary>
/// <remarks>
/// <para>
/// Problem: <br/>
///     * Need to cached byte[] so that new ones arn't created each time <br/>
///     * Arrays sent are multiple different sizes <br/>
///     * Some message might be big so need buffers to cover that size <br/>
///     * Most messages will be small compared to max message size <br/>
/// </para>
/// <br/>
/// <para>
/// Solution: <br/>
///     * Create multiple groups of buffers covering the range of allowed sizes <br/>
///     * Split range exponentially (using math.log) so that there are more groups for small buffers <br/>
/// </para>
/// </remarks>
public class BufferPool
{
    internal readonly BufferBucket[] Buckets;
    private readonly int _bucketCount;
    private readonly int _smallest;
    private readonly int _largest;


    public BufferPool(int bucketCount, int smallest, int largest)
    {
        if (bucketCount < 2)
            throw new ArgumentException("Count must be atleast 2");
        if (smallest < 1)
            throw new ArgumentException("Smallest must be atleast 1");
        if (largest < smallest)
            throw new ArgumentException("Largest must be greater than smallest");


        _bucketCount = bucketCount;
        _smallest = smallest;
        _largest = largest;


        // split range over log scale (more buckets for smaller sizes)

        double minLog = Math.Log(_smallest);
        double maxLog = Math.Log(_largest);

        double range = maxLog - minLog;
        double each = range / (bucketCount - 1);

        Buckets = new BufferBucket[bucketCount];

        for (int i = 0; i < bucketCount; i++)
        {
            double size = smallest * Math.Pow(Math.E, each * i);
            Buckets[i] = new BufferBucket((int)Math.Ceiling(size));
        }


        Validate();

        // Example
        // 5         count  
        // 20        smallest
        // 16400     largest

        // 3.0       log 20
        // 9.7       log 16400 

        // 6.7       range 9.7 - 3
        // 1.675     each  6.7 / (5-1)

        // 20        e^ (3 + 1.675 * 0)
        // 107       e^ (3 + 1.675 * 1)
        // 572       e^ (3 + 1.675 * 2)
        // 3056      e^ (3 + 1.675 * 3)
        // 16,317    e^ (3 + 1.675 * 4)

        // perceision wont be lose when using doubles
    }


    [Conditional("UNITY_ASSERTIONS")]
    private void Validate()
    {
        if (Buckets[0].ArraySize != _smallest)
            SimpleWebLog.Error($"BufferPool Failed to create bucket for smallest. bucket:{Buckets[0].ArraySize} smallest{_smallest}");

        int largestBucket = Buckets[_bucketCount - 1].ArraySize;

        // rounded using Ceiling, so allowed to be 1 more that largest
        if (largestBucket != _largest && largestBucket != _largest + 1)
            SimpleWebLog.Error($"BufferPool Failed to create bucket for largest. bucket:{largestBucket} smallest{_largest}");
    }


    public ArrayBuffer Take(int size)
    {
        if (size > _largest)
            throw new ArgumentException($"Size ({size}) is greatest that largest ({_largest})");

        for (int i = 0; i < _bucketCount; i++)
        {
            if (size <= Buckets[i].ArraySize)
                return Buckets[i].Take();
        }

        throw new ArgumentException($"Size ({size}) is greatest that largest ({_largest})");
    }
}