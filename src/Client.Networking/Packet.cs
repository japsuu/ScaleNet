namespace Client.Networking;

/// <summary>
/// A raw packet of data.
/// </summary>
public readonly struct Packet
{
    public readonly ReadOnlyMemory<byte> Data;


    public Packet(byte[] buffer, int offset, int size)
    {
        Data = new ReadOnlyMemory<byte>(buffer, offset, size);
    }
    

    public Packet(ReadOnlyMemory<byte> buffer)
    {
        Data = buffer;
    }
}