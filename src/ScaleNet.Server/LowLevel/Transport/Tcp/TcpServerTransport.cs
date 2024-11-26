using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ScaleNet.Common;
using ScaleNet.Common.Ssl;

namespace ScaleNet.Server.LowLevel.Transport.Tcp;

public class TcpServerTransport : SslServer, IServerTransport
{
    /// <summary>
    /// A raw packet of data.
    /// </summary>
    internal readonly struct Packet
    {
        public readonly ushort TypeID;
        public readonly byte[] Data;


        public Packet(ushort typeID, byte[] data)
        {
            TypeID = typeID;
            Data = data;
        }
    }
    
    private readonly ConcurrentBag<uint> _availableSessionIds = [];
    private readonly ConcurrentDictionary<SessionId, TcpClientSession> _sessions = new();
    
    private ServerState _serverState = ServerState.Stopped;
    private bool _rejectNewConnections;
    private bool _rejectNewMessages;

    public readonly IPacketMiddleware? Middleware;
    public int MaxConnections { get; }
    
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    public event Action<SessionStateChangeArgs>? SessionStateChanged;
    public event Action<SessionId, DeserializedNetMessage>? MessageReceived;


    public TcpServerTransport(SslContext sslContext, IPAddress address, int port, int maxConnections, IPacketMiddleware? middleware = null) : base(sslContext, address, port)
    {
        MaxConnections = maxConnections;
        Middleware = middleware;

        // Fill the available session IDs bag.
        for (uint i = 1; i < maxConnections; i++)
            _availableSessionIds.Add(i);
    }


    public bool StartServer()
    {
        ScaleNetManager.Logger.LogInfo($"Starting TCP transport on {Address}:{Port}...");

        bool started = Start();
        
        if (started)
            ScaleNetManager.Logger.LogInfo("TCP transport started successfully.");
        else
            ScaleNetManager.Logger.LogError("Failed to start TCP transport.");
        
        return started;
    }


    public bool StopServer(bool gracefully)
    {
        ScaleNetManager.Logger.LogInfo("Stopping TCP transport...");
        _rejectNewConnections = true;
        _rejectNewMessages = true;
        
        if (gracefully)
        {
            foreach (TcpClientSession session in _sessions.Values)
                DisconnectSession(session, DisconnectReason.ServerShutdown);
        }
        
        bool stopped = Stop();
        
        if (stopped)
            ScaleNetManager.Logger.LogInfo("TCP transport stopped successfully.");
        else
            ScaleNetManager.Logger.LogError("Failed to stop TCP transport.");
        
        return stopped;
    }


    public void HandleIncomingMessages()
    {
        if (_rejectNewMessages)
            return;
        
        //TODO: Parallelize.
        //NOTE: Sessions that are iterated first have packet priority.
        foreach (TcpClientSession session in _sessions.Values)
        {
            while (session.IncomingPackets.TryDequeue(out Packet packet))
            {
                if (!NetMessages.TryDeserialize(packet.TypeID, packet.Data, out DeserializedNetMessage msg))
                {
                    ScaleNetManager.Logger.LogWarning($"Received a packet that could not be deserialized. Kicking session {session.SessionId} immediately.");
                    DisconnectSession(session, DisconnectReason.MalformedData);
                    return;
                }
                
                MessageReceived?.Invoke(session.SessionId, msg);
            }
        }
    }
    
    
    public void HandleOutgoingMessages()
    {
        //TODO: Parallelize.
        //NOTE: Sessions that are iterated first have packet priority.
        foreach (TcpClientSession session in _sessions.Values)
        {
            SendOutgoingPackets(session);
        }
    }


    public void QueueSendAsync<T>(SessionId sessionId, T message) where T : INetMessage
    {
        if (!_sessions.TryGetValue(sessionId, out TcpClientSession? session))
        {
            ScaleNetManager.Logger.LogWarning($"Tried to send a packet to a non-existent/disconnected session with ID {sessionId}");
            return;
        }

        QueueSendAsync(session, message);
    }


    private void QueueSendAsync<T>(TcpClientSession session, T message) where T : INetMessage
    {
        if (!NetMessages.TryGetMessageId(message.GetType(), out ushort id))
        {
            ScaleNetManager.Logger.LogError($"Cannot send: failed to get the ID of message {message.GetType()}.");
            return;
        }
        
        // Write to buffer.
        byte[] bytes = NetMessages.Serialize(message);
        
        // Enqueue the packet.
        Packet p = new(id, bytes);
        
        session.OutgoingPackets.Enqueue(p);
    }


    public void DisconnectSession(SessionId sessionId, DisconnectReason reason, bool iterateOutgoing = true)
    {
        if (!_sessions.TryGetValue(sessionId, out TcpClientSession? session))
        {
            ScaleNetManager.Logger.LogWarning($"Tried to disconnect a non-existent/disconnected session with ID {sessionId}");
            return;
        }
        
        DisconnectSession(session, reason, iterateOutgoing);
    }


    internal void DisconnectSession(TcpClientSession session, DisconnectReason reason, bool iterateOutgoing = true)
    {
        if (iterateOutgoing)
        {
            QueueSendAsync(session, new DisconnectMessage(reason));
            SendOutgoingPackets(session);
        }
        
        session.Disconnect();
    }


    private void SendOutgoingPackets(TcpClientSession session)
    {
        while (session.OutgoingPackets.TryDequeue(out Packet packet))
        {
            byte[] bytes = packet.Data;
                
            Middleware?.HandleOutgoingPacket(ref bytes);
            
            // Get a pooled buffer to add the length prefix and message id.
            int messageLength = bytes.Length;
            int packetLength = messageLength + 4;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(packetLength);
            
            // Add the 16-bit packet length prefix.
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, (ushort)messageLength);
            
            // Add the 16-bit message type ID.
            ushort typeId = packet.TypeID;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(2), typeId);
            
            // Copy the message data to the buffer.
            bytes.CopyTo(buffer.AsSpan(4));
        
            session.SendAsync(buffer, 0, packetLength);
        
            // Return the buffer to the pool.
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }


#region Session Lifetime

    protected override SslSession CreateSession()
    {
        bool isIdAvailable = _availableSessionIds.TryTake(out uint uId);
        
        if (!isIdAvailable)
            throw new InvalidOperationException("No available session IDs.");
        
        SessionId id = new(uId);

        TcpClientSession session = new(id, this);
        _sessions.TryAdd(id, session);
        
        return session;
    }
    
    
    private void OnEndSession(SessionId id)
    {
        if (_sessions.TryRemove(id, out _))
            _availableSessionIds.Add(id.Value);
    }


    protected override void OnConnecting(SslSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(id, ConnectionState.Connecting));
    }


    protected override void OnConnected(SslSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(id, ConnectionState.Connected));
    }


    protected override void OnHandshaking(SslSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(id, ConnectionState.SslHandshaking));
    }


    protected override void OnHandshaked(SslSession session)
    {
        TcpClientSession tcpClientSession = (TcpClientSession)session;
        SessionId id = tcpClientSession.SessionId;
        
        if (_rejectNewConnections || _sessions.Count >= MaxConnections)
        {
            DisconnectSession(tcpClientSession, DisconnectReason.ServerFull);
            return;
        }
        
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(id, ConnectionState.Ready));
    }


    protected override void OnDisconnecting(SslSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(id, ConnectionState.Disconnecting));
    }


    protected override void OnDisconnected(SslSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(id, ConnectionState.Disconnected));
        
        OnEndSession(id);
    }

#endregion


#region Server Lifetime

    protected override void OnStarting()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Starting;
        ServerStateChanged?.Invoke(new ServerStateChangeArgs(_serverState, prevState));
    }


    protected override void OnStarted()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Started;
        ServerStateChanged?.Invoke(new ServerStateChangeArgs(_serverState, prevState));
    }

    
    protected override void OnStopping()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Stopping;
        ServerStateChanged?.Invoke(new ServerStateChangeArgs(_serverState, prevState));
    }


    protected override void OnStopped()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Stopped;
        ServerStateChanged?.Invoke(new ServerStateChangeArgs(_serverState, prevState));
    }

#endregion


    protected override void OnError(SocketError error)
    {
        ScaleNetManager.Logger.LogError($"TCP server caught an error: {error}");
    }


    protected override void Dispose(bool disposingManagedResources)
    {
        if (disposingManagedResources)
        {
            StopServer(true);
            
            foreach (TcpClientSession session in _sessions.Values)
                session.Dispose();
        }
        
        base.Dispose(disposingManagedResources);
    }
}