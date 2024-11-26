using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;

namespace ScaleNet.Server.LowLevel.Transport.Tcp;

public class NetMessageBufferWriter : IBufferWriter<byte>
{
    public int WrittenBytes { get; private set; }

    /// <summary>
    /// A thread-local, recyclable array that may be used for short bursts of code.
    /// </summary>
    [ThreadStatic]
    private static byte[]? scratchArray;


    public void Initialize(ushort messageId)
    {
        scratchArray ??= new byte[65536];
        
        BinaryPrimitives.WriteUInt16LittleEndian(scratchArray, messageId);
        
        WrittenBytes = 2;
    }
            
            
    public void CopyToAndReset(Span<byte> buffer)
    {
        scratchArray.AsSpan(0, WrittenBytes).CopyTo(buffer);
        WrittenBytes = 0;
    }
            
            
    public void Advance(int count)
    {
        Debug.Assert(WrittenBytes + count <= scratchArray?.Length, "Buffer overflow.");
        
        WrittenBytes += count;
    }


    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        scratchArray ??= new byte[65536];
        Debug.Assert(sizeHint <= scratchArray.Length, "Requested buffer size is larger than the scratch buffer.");
        
        return scratchArray.AsMemory(WrittenBytes);
    }


    public Span<byte> GetSpan(int sizeHint = 0)
    {
        scratchArray ??= new byte[65536];
        Debug.Assert(sizeHint <= scratchArray.Length, "Requested buffer size is larger than the scratch buffer.");
        
        return scratchArray.AsSpan(WrittenBytes);
    }
}