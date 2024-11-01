using System.Buffers;
using System.Diagnostics;

namespace Shared;

public readonly struct Packet // Could also be a pooled class
{
    private const int HEADER_LENGTH = 4;
    
    public readonly byte Type;
    public readonly ArraySegment<byte> Data;
    
    
    public Packet(byte[] buffer, int offset, int size)
    {
        Debug.Assert(size >= HEADER_LENGTH, "Received buffer is too short");
        
        byte type = buffer[offset + 1];
        
        ArraySegment<byte> data;
        int payloadSize = size - HEADER_LENGTH;
        if (payloadSize > 0)
        {
            byte[] dataBuffer = ArrayPool<byte>.Shared.Rent(payloadSize);

            int payloadStart = offset + HEADER_LENGTH;
            Buffer.BlockCopy(buffer, payloadStart, dataBuffer, 0, payloadSize);
        
            data = new ArraySegment<byte>(dataBuffer, 0, payloadSize);
        }
        else
        {
            data = [];
        }
        
        Type = type;
        Data = data;
    }
}