using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using ScaleNet.Networking;
using ScaleNet.Utils;

namespace ScaleNet.Server.LowLevel.Transport.Tcp;

public class TcpServerTransport : TcpServer, IServerTransport
{
    /// <summary>
    /// A raw packet of data.
    /// </summary>
    internal readonly struct Packet
    {
        public readonly Memory<byte> Data;


        public Packet(byte[] buffer, int offset, int size)
        {
            Data = new Memory<byte>(buffer, offset, size);
        }
        
        
        public Packet(Memory<byte> span)
        {
            Data = span;
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
    public event Action<SessionId, INetMessage>? HandleMessage;


    public TcpServerTransport(IPAddress address, int port, int maxConnections, IPacketMiddleware? middleware = null) : base(address, port)
    {
        MaxConnections = maxConnections;
        Middleware = middleware;

        // Fill the available session IDs bag.
        for (uint i = 1; i < maxConnections; i++)
            _availableSessionIds.Add(i);
    }
    
    
    bool IServerTransport.Start()
    {
        Logger.LogInfo($"Starting TCP transport on {Address}:{Port}...");

        bool started = Start();
        
        if (started)
            Logger.LogInfo("TCP transport started successfully.");
        else
            Logger.LogError("Failed to start TCP transport.");
        
        return started;
    }
    
    
    bool IServerTransport.Stop(bool gracefully)
    {
        Logger.LogInfo("Stopping TCP transport...");
        _rejectNewConnections = true;
        _rejectNewMessages = true;
        
        if (gracefully)
        {
            foreach (TcpClientSession session in _sessions.Values)
                DisconnectSession(session, DisconnectReason.ServerShutdown);
        }
        
        bool stopped = Stop();
        
        if (stopped)
            Logger.LogInfo("TCP transport stopped successfully.");
        else
            Logger.LogError("Failed to stop TCP transport.");
        
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
                INetMessage? msg = NetMessages.Deserialize(packet.Data);
        
                if (msg == null)
                {
                    Logger.LogWarning($"Received a packet that could not be deserialized. Kicking session {session.SessionId} immediately.");
                    DisconnectSession(session, DisconnectReason.MalformedData);
                    return;
                }
                
                HandleMessage?.Invoke(session.SessionId, msg);
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
            Logger.LogWarning($"Tried to send a packet to a non-existent/disconnected session with ID {sessionId}");
            return;
        }

        QueueSendAsync(session, message);
    }


    private static void QueueSendAsync<T>(TcpClientSession session, T message) where T : INetMessage
    {
        // Write to buffer.
        byte[] bytes = NetMessages.Serialize(message);
        
        // Enqueue the packet.
        Packet p = new(bytes, 0, bytes.Length);
        
        session.OutgoingPackets.Enqueue(p);
    }


    public void DisconnectSession(SessionId sessionId, DisconnectReason reason, bool iterateOutgoing = true)
    {
        if (!_sessions.TryGetValue(sessionId, out TcpClientSession? session))
        {
            Logger.LogWarning($"Tried to disconnect a non-existent/disconnected session with ID {sessionId}");
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
            Memory<byte> buffer = packet.Data;
                
            Middleware?.HandleOutgoingPacket(ref buffer);
        
            // Get a pooled buffer, and add the 16-bit packet length prefix.
            int packetLength = buffer.Length + 2;
            byte[] data = ArrayPool<byte>.Shared.Rent(packetLength);
            BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)buffer.Length);
            buffer.Span.CopyTo(data.AsSpan(2));
        
            session.SendAsync(data, 0, packetLength);
        
            // Return the buffer to the pool.
            ArrayPool<byte>.Shared.Return(data);
        }
    }


#region Session Lifetime

    protected override TcpSession CreateSession()
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


    protected override void OnConnecting(TcpSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(id, ConnectionState.Connecting));
    }
    

    protected override void OnConnected(TcpSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(id, ConnectionState.Connected));
        
        //TODO: Figure out an way to reject new connections earlier.
        if (_rejectNewConnections || _sessions.Count >= MaxConnections)
            session.Disconnect();
    }


    protected override void OnDisconnecting(TcpSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(id, ConnectionState.Disconnecting));
    }


    protected override void OnDisconnected(TcpSession session)
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
        Logger.LogError($"TCP server caught an error: {error}");
    }
}