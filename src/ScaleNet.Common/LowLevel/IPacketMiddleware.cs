namespace ScaleNet.Common.LowLevel
{
    /// <summary>
    /// Represents a middleware that can pre-process incoming and outgoing packets.<br/>
    /// This can be used to implement packet encryption, compression, etc.
    /// </summary>
    public interface IPacketMiddleware
    {
        public void HandleIncomingPacket(ref NetMessagePacket buffer);
        public void HandleOutgoingPacket(ref NetMessagePacket buffer);
    }
}