namespace Shared.Networking;

public readonly struct Packet(byte[] buffer, int offset, int size)
{
    public readonly ArraySegment<byte> Data = new(buffer, offset, size);
}