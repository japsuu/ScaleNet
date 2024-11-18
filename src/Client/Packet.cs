namespace Client;

/// <summary>
/// A raw packet of data.
/// </summary>
public readonly struct Packet
{
    public readonly Memory<byte> Data;


    public Packet(byte[] buffer, int offset, int size)
    {
        Data = new Memory<byte>(buffer, offset, size);
    }
    

    public Packet(Memory<byte> buffer)
    {
        Data = buffer;
    }
}