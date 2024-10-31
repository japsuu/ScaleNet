using System.Buffers;
using System.Diagnostics;

namespace Server;

internal readonly struct Packet // Could also be a pooled class
{
    private const byte FORMAT_VERSION = 1;
    private const int HEADER_LENGTH = 4;
    
    public readonly Guid PlayerSessionId;
    public readonly byte Version;
    public readonly byte Type;
    public readonly ArraySegment<byte> Data;
    
    
    private Packet(Guid playerSessionId, byte version, byte type, ArraySegment<byte> data)
    {
        PlayerSessionId = playerSessionId;
        Version = version;
        Type = type;
        Data = data;
    }
    
    
    public static bool TryCreate(Guid playerSessionId, byte[] buffer, int offset, int size, out Packet packet)
    {
        Debug.Assert(size >= HEADER_LENGTH, "Received buffer is too short");
        
        byte version = buffer[offset];
        if (version != FORMAT_VERSION)
        {
            packet = default;
            return false;
        }
        
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
        
        packet = new Packet(playerSessionId, version, type, data);
        
        return true;
    }
}