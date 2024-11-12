namespace Shared.Networking;

/// <summary>
/// Represents a middleware that can pre-process incoming and outgoing packets.<br/>
/// This can be used to implement packet encryption, compression, etc.
/// </summary>
public interface IPacketMiddleware
{
    public void HandleIncomingPacket(ref ReadOnlyMemory<byte> buffer);
    public void HandleOutgoingPacket(ref ReadOnlyMemory<byte> buffer);
}