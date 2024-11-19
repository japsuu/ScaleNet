namespace ScaleNet.Networking
{
    /// <summary>
    /// Represents a middleware that can pre-process incoming and outgoing packets.<br/>
    /// This can be used to implement packet encryption, compression, etc.
    /// </summary>
    public interface IPacketMiddleware
    {
        public void HandleIncomingPacket(ref Memory<byte> buffer);
        public void HandleOutgoingPacket(ref Memory<byte> buffer);
    }
}