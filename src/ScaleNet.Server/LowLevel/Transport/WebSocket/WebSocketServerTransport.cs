using System.Net;
using ScaleNet.Common;
using ScaleNet.Common.LowLevel;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket;

public sealed class WebSocketServerTransport : IServerTransport
{
    /// <summary>
    /// Minimum UDP packet size allowed.
    /// </summary>
    private const int MINIMUM_MTU = 576;
    
    /// <summary>
    /// Maximum UDP packet size allowed.
    /// </summary>
    private const int MAXIMUM_MTU = ushort.MaxValue;
    
    private readonly FishNet.Transporting.Bayou.Server.ServerSocket _serverSocket = new();
    private readonly ServerSslContext _sslContext;
    private readonly int _mtu;

    public ushort Port { get; }
    public int MaxConnections { get; }
    public ServerState State => _serverSocket.GetConnectionState();
    
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    public event Action<SessionStateChangeArgs>? SessionStateChanged;
    public event Action<SessionId, DeserializedNetMessage>? MessageReceived;


    public WebSocketServerTransport(ServerSslContext sslContext, ushort port, int maxConnections, int mtu = 1023)
    {
        if (_mtu < 0)
            _mtu = MINIMUM_MTU;
        else if (_mtu > MAXIMUM_MTU)
            _mtu = MAXIMUM_MTU;
        
        _sslContext = sslContext;
        _mtu = mtu;
        Port = port;
        MaxConnections = maxConnections;
    }
    
    
    public void Dispose()
    {
        StopServer(true);
    }
    
    
    //WARN: Replace with event subscriptions
    public void HandleServerConnectionState(ServerStateChangeArgs connectionStateArgs) => ServerStateChanged?.Invoke(connectionStateArgs);
    public void HandleRemoteConnectionState(SessionStateChangeArgs connectionStateArgs) => SessionStateChanged?.Invoke(connectionStateArgs);
    public void HandleServerReceivedDataArgs(SessionId from, DeserializedNetMessage msg) => MessageReceived?.Invoke(from, msg);


#region Starting and stopping

    public bool StartServer()
    {
        ScaleNetManager.Logger.LogInfo($"Starting WS transport on port {Port}...");
        _serverSocket.Initialize(this, _mtu, _sslContext);

        bool started = _serverSocket.StartConnection(Port, MaxConnections);
        
        if (started)
            ScaleNetManager.Logger.LogInfo("TCP transport started successfully.");
        else
            ScaleNetManager.Logger.LogError("Failed to start TCP transport.");
        
        return started;
    }


    public bool StopServer(bool gracefully = true)
    {
        ScaleNetManager.Logger.LogInfo("Stopping WS transport...");
        
        if (gracefully)
        {
            foreach (TcpClientSession session in _sessions.Values)
                DisconnectSession(session, InternalDisconnectReason.ServerShutdown);
        }
        
        bool stopped = _serverSocket.StopConnection();
        
        if (stopped)
            ScaleNetManager.Logger.LogInfo("WS transport stopped successfully.");
        else
            ScaleNetManager.Logger.LogError("Failed to stop WS transport.");
        
        return stopped;
    }

#endregion


#region Message iterating

    public void HandleIncomingMessages()
    {
        _serverSocket.IterateIncoming();
    }


    public void HandleOutgoingMessages()
    {
        _serverSocket.IterateOutgoing();
    }

#endregion


#region Sending

    public void QueueSendAsync<T>(SessionId sessionId, T message) where T : INetMessage
    {
        _serverSocket.SendToClient(channelId, segment, connectionId);
    }

#endregion


#region Utils

    public void DisconnectSession(SessionId sessionId, InternalDisconnectReason reason, bool iterateOutgoing = true)
    {
        bool success = _serverSocket.StopConnection(sessionId, !iterateOutgoing);
    }


    public ConnectionState GetConnectionState(SessionId sessionId)
    {
        return _serverSocket.GetConnectionState(sessionId);
    }
    
    
    public EndPoint? GetClientEndPoint(SessionId sessionId)
    {
        return _serverSocket.GetConnectionAddress(sessionId);
    }

#endregion
}