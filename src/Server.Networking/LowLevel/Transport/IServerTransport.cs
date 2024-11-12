namespace Server.Networking.LowLevel.Transport;

public interface IServerTransport
{
    public int Port { get; }
    public int MaxConnections { get; }
    public bool RejectNewConnections { get; set; }
    public bool RejectNewPackets { get; set; }
    
    public event Action<ServerStateArgs>? ServerStateChanged;
    
    /// <summary>
    /// Called when the connection state of a client changes.
    /// </summary>
    public event Action<SessionStateArgs>? SessionStateChanged;
    
    /// <summary>
    /// Called to handle incoming packets.<br/>
    /// Implementations are required to be thread-safe, as this event may be raised from multiple threads.
    /// </summary>
    public event Action<Packet>? HandlePacket;


    /// <summary>
    /// Queues the given packet to be sent.
    /// The buffer will not be sent immediately, but the next time outgoing packets are iterated.
    /// </summary>
    /// <param name="packet">The packet to send. Contents are copied internally.</param>
    public void QueueSendAsync(Packet packet);
    
    public void HandleIncomingPackets();
    public void HandleOutgoingPackets();
}