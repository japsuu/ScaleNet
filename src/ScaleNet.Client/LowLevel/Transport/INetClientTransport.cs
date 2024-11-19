namespace ScaleNet.Client.LowLevel.Transport
{
    public interface INetClientTransport
    {
        public string Address { get; }
        public int Port { get; }
    
        public void Connect();
        public void Reconnect();
        public void Disconnect();

        /// <summary>
        /// Sends the given buffer to the server asynchronously.
        /// </summary>
        /// <param name="buffer">The buffer to send. Contents are copied internally.</param>
        public void SendAsync(Memory<byte> buffer);
    
        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        public event Action<ConnectionStateArgs>? ConnectionStateChanged;
    
        /// <summary>
        /// Called when a packet is received from the server.
        /// </summary>
        public event Action<Packet>? PacketReceived;
    }
}