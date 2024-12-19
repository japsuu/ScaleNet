﻿using System;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.Core
{
    internal struct Packet
    {
        public readonly int ConnectionId;
        public byte[] Data;
        public int Length;
        public readonly byte Channel;


        public Packet(int connectionId, byte[] data, int length, byte channel)
        {
            ConnectionId = connectionId;
            Data = data;
            Length = length;
            Channel = channel;
        }


        public Packet(int sender, ArraySegment<byte> segment, byte channel)
        {
            Data = ByteArrayPool.Retrieve(segment.Count);
            Buffer.BlockCopy(segment.Array, segment.Offset, Data, 0, segment.Count);
            ConnectionId = sender;
            Length = segment.Count;
            Channel = channel;
        }


        public ArraySegment<byte> GetArraySegment() => new(Data, 0, Length);


        /// <summary>
        /// Adds on length and resizes Data if needed.
        /// </summary>
        /// <param name="length"></param>
        public void AddLength(int length)
        {
            int totalNeeded = Length + length;
            if (Data.Length < totalNeeded)
                Array.Resize(ref Data, totalNeeded);

            Length += length;
        }


        public void Dispose()
        {
            ByteArrayPool.Store(Data);
        }
    }
}