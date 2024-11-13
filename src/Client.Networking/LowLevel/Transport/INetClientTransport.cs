namespace Client.Networking.LowLevel.Transport;

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
    public void SendAsync(ReadOnlyMemory<byte> buffer);

    /// <summary>
    /// Iterates the incoming packets.
    /// </summary>
    public void IterateIncomingPackets();
    
    /// <summary>
    /// Called after the local client connection state changes.
    /// </summary>
    public event Action<ConnectionStateArgs>? ConnectionStateChanged;
    
    /// <summary>
    /// Called when a packet is received from the server.
    /// </summary>
    public event Action<Packet>? PacketReceived;
}