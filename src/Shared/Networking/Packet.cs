namespace Shared.Networking;

/// <summary>
/// A raw packet of data.
/// TODO: Packet memory pooling.
/// </summary>
public readonly struct Packet(byte[] buffer, int offset, int size)
{
    public readonly ArraySegment<byte> Data = new(buffer, offset, size);    //TODO: Change to ReadOnlyMemory<byte>
}