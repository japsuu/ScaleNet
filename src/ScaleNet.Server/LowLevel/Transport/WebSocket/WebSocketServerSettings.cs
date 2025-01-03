using ScaleNet.Common;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket;

public class WebSocketServerSettings
{
    /// <summary>
    /// Minimum MTU allowed.
    /// </summary>
    private const int MINIMUM_MTU = 576;
    
    /// <summary>
    /// Maximum MTU allowed.
    /// </summary>
    private const int MAXIMUM_MTU = ushort.MaxValue;

    /// <summary>
    /// The port to listen on.
    /// </summary>
    public ushort Port { get; private set; } = 11221;
    
    /// <summary>
    /// Maximum number of connections allowed.
    /// </summary>
    public int MaxConnections { get; private set; } = 1000;

    /// <summary>
    /// Maximum data fragment size in bytes.
    /// Used to initialize internal buffers.
    /// If a packet is larger than this, it will always be sent in multiple fragments.
    /// </summary>
    public int MaxFragmentSize { get; private set; } = SharedConstants.MAX_PACKET_SIZE_BYTES;
    
    /// <summary>
    /// Whether to disable the Nagle algorithm.
    /// True to send packets immediately, rather than wait for more data to send in the same packet.
    /// </summary>
    public bool NoDelay { get; private set; } = false;
    
    /// <summary>
    /// The amount of time the server will wait for a send operation to complete before timing out.
    /// </summary>
    public int SendTimeout { get; private set; } = 5000;
    
    /// <summary>
    /// The amount of time the server will wait for a receive operation to complete before timing out.
    /// </summary>
    public int ReceiveTimeout { get; private set; } = 20000;
    
    /// <summary>
    /// The maximum number of messages the server will process in a single tick.
    /// </summary>
    public int MaxMessagesPerTick { get; private set; } = 10000;
    
    /// <summary>
    /// The maximum size of a handshake message in bytes.
    /// Used to initialize internal buffers.
    /// </summary>
    public int HandshakeMaxSize { get; private set; } = 5000;

    /// <summary>
    /// The SSL context to use for the server.
    /// </summary>
    public ServerSslContext? SslContext { get; private set; } = null;


    public WebSocketServerSettings WithPort(ushort port)
    {
        Port = port;
        return this;
    }
    
    
    public WebSocketServerSettings WithMaxConnections(int maxConnections)
    {
        MaxConnections = maxConnections;
        return this;
    }
    
    
    public WebSocketServerSettings WithMaxFragmentSize(int maxFragmentSize)
    {
        maxFragmentSize = Math.Clamp(maxFragmentSize, MINIMUM_MTU, MAXIMUM_MTU);

        MaxFragmentSize = maxFragmentSize;
        return this;
    }
    
    
    public WebSocketServerSettings WithSslContext(ServerSslContext sslContext)
    {
        SslContext = sslContext;
        return this;
    }
}