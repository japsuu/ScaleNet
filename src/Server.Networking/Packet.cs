using Server.Networking.HighLevel;

namespace Server.Networking;

public readonly struct Packet
{
    public readonly SessionId SessionId;
    public readonly ReadOnlyMemory<byte> Data;


    public Packet(SessionId sessionId, byte[] buffer, int offset, int size)
    {
        SessionId = sessionId;
        Data = new ReadOnlyMemory<byte>(buffer, offset, size);
    }


    public Packet(SessionId sessionId, ReadOnlyMemory<byte> data)
    {
        SessionId = sessionId;
        Data = data;
    }
}