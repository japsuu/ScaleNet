using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ScaleNet.Common.Transport.Utils;
#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif

namespace ScaleNet.Common.Transport.Components
{
    /*
     * Concurrent bag has a TLS list ( ThreadLocal<ThreadLocalList> m_locals )
     * each bucket holds a set of weak references to byte arrays
     * these arrays are pooled and reusable, and we preserve the peak memory usage by this.
     * If application calls the GC gen2 collect some of these weak references are cleared,
     * this way we trim the pools automatically if they are not referenced by the application.
     *
     * you can also configure the pool to auto GC collect(also does gen2) if the application is mostly idle and
     * we reached to some threshold on working set memory.
     */
    public static class BufferPool
    {
        public const int MAX_BUFFER_SIZE = 1073741824;
        public const int MIN_BUFFER_SIZE = 256;
        private static readonly ConcurrentDictionary<byte[], byte> BufferDuplicateMap = new();
        private static readonly ConcurrentBag<byte[]>[] BufferBuckets = new ConcurrentBag<byte[]>[32];

        private static readonly SortedDictionary<int, int> BucketCapacityLimits = new()
        {
            { 256, 10000 },
            { 512, 10000 },
            { 1024, 10000 },
            { 2048, 5000 },
            { 4096, 1000 },
            { 8192, 1000 },
            { 16384, 500 },
            { 32768, 300 },
            { 65536, 300 },
            { 131072, 200 },
            { 262144, 50 },
            { 524288, 10 },
            { 1048576, 4 },
            { 2097152, 2 },
            { 4194304, 1 },
            { 8388608, 1 },
            { 16777216, 1 },
            { 33554432, 0 },
            { 67108864, 0 },
            { 134217728, 0 },
            { 268435456, 0 },
            { 536870912, 0 },
            { 1073741824, 0 }
        };


        static BufferPool()
        {
            Init();
            
            Task.Run(MaintainMemory);
        }


        // creates bufferBuckets structure
        private static void Init()
        {
            //bufferBuckets = new ConcurrentDictionary<int, ConcurrentBag<byte[]>>();
            for (int i = 8; i < 31; i++)
                BufferBuckets[i] = new ConcurrentBag<byte[]>();
        }


        private static async Task MaintainMemory()
        {
            while (true)
            {
                await Task.Delay(10000);
                for (int i = 8; i < 31; i++)
                {
                    while (BufferBuckets[i].Count > BucketCapacityLimits[GetBucketSize(i)])
                    {
                        if (BufferBuckets[i].TryTake(out byte[]? buffer))
                            BufferDuplicateMap.TryRemove(buffer, out _);
                    }
                }
            }
        }


        /// <summary>
        ///     Rents a buffer at least the size requested
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] RentBuffer(int size)
        {
            if (MAX_BUFFER_SIZE < size)
            {
                throw new InvalidOperationException($"Unable to rent buffer bigger than max buffer size: {MAX_BUFFER_SIZE}");
            }

            if (size <= MIN_BUFFER_SIZE)
                return new byte[size];

            int idx = GetBucketIndex(size);

            if (BufferBuckets[idx].TryTake(out byte[] buffer))
            {
                BufferDuplicateMap.TryRemove(buffer, out _);
                return buffer;
            }

            buffer = ByteCopy.GetNewArray(GetBucketSize(idx));
            return buffer;
        }


        /// <summary>
        ///     Return rented buffer. Take care not to return twice!
        /// </summary>
        /// <param name="buffer"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnBuffer(byte[] buffer)
        {
            if (buffer.Length <= MIN_BUFFER_SIZE)
                return;

            if (!BufferDuplicateMap.TryAdd(buffer, 0))
            {
                TransportLogger.Log(TransportLogger.LogLevel.Error, "Buffer Pool Duplicated return detected");
                return;
            }

            int idx = GetBucketIndex(buffer.Length);
            BufferBuckets[idx - 1].Add(buffer);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBucketSize(int bucketIndex) => 1 << bucketIndex;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetBucketIndex(int size) => 32 - LeadingZeros(size);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LeadingZeros(int x)
        {
#if NET5_0_OR_GREATER
            if (Lzcnt.IsSupported)
            {
                // LZCNT contract is 0->32
                return (int)Lzcnt.LeadingZeroCount((uint)x);
            }

            if (ArmBase.IsSupported)
            {
                return ArmBase.LeadingZeroCount(x);
            }
            else
            {
             const int numIntBits = sizeof(int) * 8;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            //count the ones
            x -= x >> 1 & 0x55555555;
            x = (x >> 2 & 0x33333333) + (x & 0x33333333);
            x = (x >> 4) + x & 0x0f0f0f0f;
            x += x >> 8;
            x += x >> 16;
            return numIntBits - (x & 0x0000003f); //subtract # of 1s from 32
            }


#else
            const int numIntBits = sizeof(int) * 8;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;

            //count the ones
            x -= (x >> 1) & 0x55555555;
            x = ((x >> 2) & 0x33333333) + (x & 0x33333333);
            x = ((x >> 4) + x) & 0x0f0f0f0f;
            x += x >> 8;
            x += x >> 16;
            return numIntBits - (x & 0x0000003f); //subtract # of 1s from 32
#endif
        }
    }
}