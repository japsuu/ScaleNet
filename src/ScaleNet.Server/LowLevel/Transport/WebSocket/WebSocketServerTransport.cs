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
    private readonly IPacketMiddleware? _middleware;
    private readonly int _mtu;  //WARN: IDK if necessary with a WS transport

    public ushort Port { get; }
    public int MaxConnections { get; }
    public ServerState State => _serverSocket.State;
    
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    public event Action<SessionStateChangeArgs>? SessionStateChanged;
    public event Action<SessionId, DeserializedNetMessage>? MessageReceived;


    public WebSocketServerTransport(ServerSslContext sslContext, ushort port, int maxConnections, int mtu = 1023, IPacketMiddleware? middleware = null)
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
        _serverSocket.ServerStateChanged += a => ServerStateChanged?.Invoke(a);
        _serverSocket.SessionStateChanged += a => SessionStateChanged?.Invoke(a);
        _serverSocket.MessageReceived += HandleServerReceivedData;
        
        _middleware = middleware;
    }
    
    
    public void Dispose()
    {
        StopServer();
        
        _serverSocket.Dispose();
    }


    private void HandleServerReceivedData(SessionId from, ArraySegment<byte> data)
    {
        /*
         TODO: Implement too-many-packets check with WS transport
         if (IncomingPackets.Count > ServerConstants.MAX_PACKETS_PER_TICK)
        {
            ScaleNetManager.Logger.LogWarning($"Session {from} is sending too many packets. Kicking immediately.");
            StopConnection(from, InternalDisconnectReason.TooManyPackets);
            return;
        }*/
        
        if (data.Count > SharedConstants.MAX_MESSAGE_SIZE_BYTES)
        {
            ScaleNetManager.Logger.LogWarning($"Session {from} sent a packet that is too large. Kicking immediately.");
            StopConnection(from, InternalDisconnectReason.OversizedPacket);
            return;
        }
        
        NetMessagePacket packet = NetMessagePacket.CreateIncoming(data);
        
        _middleware?.HandleIncomingPacket(ref packet);
        
        MessageReceived?.Invoke(from, msg);
    }


#region Starting and stopping

    public bool StartServer()
    {
        ScaleNetManager.Logger.LogInfo($"Starting WS transport on port {Port}...");
        _serverSocket.Initialize(_mtu, _sslContext);

        bool started = _serverSocket.StartServer(Port, MaxConnections);
        
        if (started)
            ScaleNetManager.Logger.LogInfo("WS transport started successfully.");
        else
            ScaleNetManager.Logger.LogError("Failed to start WS transport.");
        
        return started;
    }


    public bool StopServer()
    {
        ScaleNetManager.Logger.LogInfo("Stopping WS transport...");
        
        foreach (SessionId session in _serverSocket.ConnectedClients)
            StopConnection(session, InternalDisconnectReason.ServerShutdown);
        
        // Iterate outgoing to ensure all disconnection messages are sent.
        IterateOutgoingMessages();
        
        bool stopped = _serverSocket.StopServer();
        
        if (stopped)
            ScaleNetManager.Logger.LogInfo("WS transport stopped successfully.");
        else
            ScaleNetManager.Logger.LogError("Failed to stop WS transport.");
        
        return stopped;
    }

#endregion


#region Message iterating

    public void IterateIncomingMessages()
    {
        _serverSocket.IterateIncoming();
    }


    public void IterateOutgoingMessages()
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

    public bool StopConnection(SessionId sessionId, InternalDisconnectReason reason)
    {
        QueueSendAsync(sessionId, new InternalDisconnectMessage(reason));
        
        return DisconnectSession(sessionId, true);
    }

    public bool StopConnectionImmediate(SessionId sessionId)
    {
        return DisconnectSession(sessionId, false);
    }


    private bool DisconnectSession(SessionId sessionId, bool iterateOutgoing)
    {
        bool success = _serverSocket.StopConnection(sessionId, iterateOutgoing);
        
        if (!success)
            ScaleNetManager.Logger.LogError($"Failed to disconnect session {sessionId}.");
        
        return success;
    }


    public ConnectionState GetConnectionState(SessionId sessionId)
    {
        return _serverSocket.GetConnectionState(sessionId);
    }
    
    
    public EndPoint? GetClientEndPoint(SessionId sessionId)
    {
        return _serverSocket.GetConnectionEndPoint(sessionId);
    }

#endregion
}