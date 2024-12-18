using System.Diagnostics;
using System.Net;
using ScaleNet.Common;
using ScaleNet.Common.LowLevel;
using ScaleNet.Server.LowLevel.Transport.WebSocket.Core;

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
    
    private readonly ServerSocket _serverSocket;
    private readonly ServerSslContext _sslContext;
    private readonly int _mtu;  //WARN: IDK if necessary with a WS transport

    public ushort Port { get; }
    public int MaxConnections { get; }
    public ServerState State => _serverSocket.State;
    
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
        
        _serverSocket = new ServerSocket();
        _serverSocket.ServerStateChanged += HandleServerConnectionState;
        _serverSocket.SessionStateChanged += HandleRemoteConnectionState;
        _serverSocket.MessageReceived += HandleServerReceivedDataArgs;
    }
    
    
    public void Dispose()
    {
        StopServer(true);
    }
    
    
    private void HandleServerConnectionState(ServerStateChangeArgs connectionStateArgs) => ServerStateChanged?.Invoke(connectionStateArgs);
    private void HandleRemoteConnectionState(SessionStateChangeArgs connectionStateArgs) => SessionStateChanged?.Invoke(connectionStateArgs);


    private void HandleServerReceivedDataArgs(SessionId from, ArraySegment<byte> data)
    {
        
        
        MessageReceived?.Invoke(from, msg);
    }


#region Starting and stopping

    public bool StartServer()
    {
        ScaleNetManager.Logger.LogInfo($"Starting WS transport on port {Port}...");
        _serverSocket.Initialize(_mtu, _sslContext);

        bool started = _serverSocket.StartServer(Port, MaxConnections);
        
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
        
        bool stopped = _serverSocket.StopServer();
        
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
        Debug.Assert(sessionId != SessionId.Invalid, "Invalid session ID.");
        
        // Write to a packet.
        if (!NetMessages.TrySerialize(message, out NetMessagePacket packet))
            return;

        // _serverSocket internally handles the broadcast ID.
        _serverSocket.QueueSend(sessionId, packet);
    }

#endregion


#region Utils

    public void DisconnectSession(SessionId sessionId, InternalDisconnectReason reason, bool iterateOutgoing = true)
    {
        bool success = _serverSocket.StopConnection(sessionId, !iterateOutgoing);
        
        if (!success)
            ScaleNetManager.Logger.LogError($"Failed to disconnect session {sessionId}.");
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