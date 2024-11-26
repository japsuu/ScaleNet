using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using ScaleNet.Common;
using ScaleNet.Common.Ssl;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Base.Core;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.SSL.ByteMessage;
using ScaleNet.Server.LowLevel.Transport.Tcp;

namespace ScaleNet.Server.LowLevel.Transport.TCP.StandardNetworkLibrary;

public sealed class TcpServerTransport : IServerTransport
{
    private readonly SslByteMessageServer _server;
    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly NetMessageBufferWriter _writer = new();
    
    private ServerState _serverState = ServerState.Stopped;
    private bool _isShuttingDown;

    public int Port { get; set; }
    public int MaxConnections { get; }
    
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    public event Action<SessionStateChangeArgs>? SessionStateChanged;
    public event Action<Guid, DeserializedNetMessage>? MessageReceived;


    public TcpServerTransport(SslContext sslContext, int port, int maxConnections)
    {
        Port = port;
        MaxConnections = maxConnections;
        
        _server = new SslByteMessageServer(Port, sslContext.Certificate);
        _server.RemoteCertificateValidationCallback = sslContext.CertificateValidationCallback;
        _server.GatherConfig = ScatterGatherConfig.UseBuffer;
        
        _server.OnBytesReceived += OnBytesReceived;
        _server.OnClientRequestedConnection += OnClientRequestedConnection;
        _server.OnClientAccepted += OnClientConnected;
        _server.OnClientDisconnected += OnClientDisconnected;
    }


    private void OnBytesReceived(Guid session, byte[] bytes, int offset, int count)
    {
        // Framing is handled automatically by SslByteMessageClient.
        
        // Get the session's packet queue.
        if (!_sessions.TryGetValue(session, out Session? queue))
        {
            ScaleNetManager.Logger.LogError($"No packet queue found for session {session}. Skipping.");
            return;
        }
        
        // Check for packet spam.
        if (queue.IncomingMessages.Count > ServerConstants.MAX_PACKETS_PER_TICK)
        {
            ScaleNetManager.Logger.LogWarning($"Session {session} is sending too many packets. Kicking immediately.");
            DisconnectSession(session, DisconnectReason.TooManyPackets);
            return;
        }
        
        // Check for oversized packets.
        if (count > SharedConstants.MAX_PACKET_SIZE_BYTES)
        {
            ScaleNetManager.Logger.LogWarning($"Session {session} sent a packet that is too large. Kicking immediately.");
            DisconnectSession(session, DisconnectReason.OversizedPacket);
            return;
        }
            
        // Ensure the message is at least 2 bytes long.
        if (count < 2)
        {
            ScaleNetManager.Logger.LogWarning($"Received a message without a type ID. Kicking session {session} immediately.");
            DisconnectSession(session, DisconnectReason.MalformedData);
            return;
        }
            
        // Extract message type ID from the first 2 bytes.
        ushort typeId = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
            
        // Extract the message data as read-only memory.
        ReadOnlyMemory<byte> memory = new(bytes, offset + 2, count - 2);
            
        if (!NetMessages.TryDeserialize(typeId, memory, out DeserializedNetMessage message))
        {
            ScaleNetManager.Logger.LogWarning($"Received a packet that could not be deserialized. Kicking session {session} immediately.");
            DisconnectSession(session, DisconnectReason.MalformedData);
            return;
        }
        
        queue.IncomingMessages.Enqueue(message);
    }


    public void StartServer()
    {
        ScaleNetManager.Logger.LogInfo($"Starting TCP transport on {Port}...");
        
        OnStarting();

        _server.StartServer();
        
        OnStarted();
    }


    public void StopServer(bool gracefully)
    {
        _isShuttingDown = true;
        ScaleNetManager.Logger.LogInfo("Stopping TCP transport...");
        
        OnStopping();
        
        if (gracefully)
        {
            foreach (Guid session in _sessions.Keys)
                DisconnectSession(session, DisconnectReason.ServerShutdown);
        }
        
        _server.ShutdownServer();
        
        OnStopped();
    }


    public void HandleIncomingMessages()
    {
        if (_isShuttingDown)
            return;
        
        //TODO: Parallelize.
        //NOTE: Sessions that are iterated first have packet priority.
        foreach ((Guid sessionId, Session? session) in _sessions)
        {
            while (session.IncomingMessages.TryDequeue(out DeserializedNetMessage msg))
            {
                MessageReceived?.Invoke(sessionId, msg);
            }
        }
    }
    
    
    public void HandleOutgoingMessages()
    {
        //TODO: Parallelize.
        //NOTE: Sessions that are iterated first have packet priority.
        foreach ((Guid id, Session? session) in _sessions)
        {
            SendOutgoingPackets(id, session);
        }
    }


    public void QueueSendAsync<T>(Guid sessionId, T message) where T : INetMessage
    {
        if (!_sessions.TryGetValue(sessionId, out Session? session))
        {
            ScaleNetManager.Logger.LogWarning($"Tried to send a packet to a non-existent/disconnected session with ID {sessionId}");
            return;
        }

        QueueSendAsync(session, message);
    }


    private void QueueSendAsync<T>(Session session, T message) where T : INetMessage
    {
        if (!NetMessages.TryGetMessageId(message.GetType(), out ushort id))
        {
            ScaleNetManager.Logger.LogError($"Cannot send: failed to get the ID of message {message.GetType()}.");
            return;
        }
        
        // Write to buffer.
        _writer.Initialize(id);
        
        NetMessages.Serialize(message, _writer);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(_writer.WrittenBytes);
        
        _writer.CopyToAndReset(bytes);
        
        // Enqueue the packet.
        SerializedNetMessage p = new(bytes);
        
        session.OutgoingPackets.Enqueue(p);
    }


    public void DisconnectSession(Guid sessionId, DisconnectReason reason, bool iterateOutgoing = true)
    {
        if (!_sessions.TryGetValue(sessionId, out Session? session))
        {
            ScaleNetManager.Logger.LogWarning($"Tried to send packets to a non-existent/disconnected session with ID {sessionId}");
            return;
        }
        
        if (iterateOutgoing)
        {
            QueueSendAsync(session, new DisconnectMessage(reason));
            SendOutgoingPackets(sessionId, session);
        }
        
        _server.CloseSession(sessionId);
    }


    private void SendOutgoingPackets(Guid sessionId, Session session)
    {
        while (session.OutgoingPackets.TryDequeue(out SerializedNetMessage packet))
        {
            _server.SendBytesToClient(sessionId, packet.Data);
            
            packet.Dispose();
        }
    }


#region Session Lifetime


    private void OnClientConnected(Guid guid)
    {
        _sessions.TryAdd(guid, new Session());
        
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(guid, ConnectionState.Connected));
    }


    private void OnClientDisconnected(Guid guid)
    {
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(guid, ConnectionState.Disconnected));
        
        bool removed = _sessions.TryRemove(guid, out Session? queue);
        if (removed)
        {
            queue!.Dispose();
        }
        Debug.Assert(removed, "Failed to remove packet queue for disconnected session.");
    }


    private bool OnClientRequestedConnection(Socket socket)
    {
        //TODO: Send a message to the client if the server is full.
        return !_isShuttingDown && _sessions.Count < MaxConnections;
    }

#endregion


#region Server Lifetime

    private void OnStarting()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Starting;
        ServerStateChanged?.Invoke(new ServerStateChangeArgs(_serverState, prevState));
    }


    private void OnStarted()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Started;
        ServerStateChanged?.Invoke(new ServerStateChangeArgs(_serverState, prevState));
    }

    
    private void OnStopping()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Stopping;
        ServerStateChanged?.Invoke(new ServerStateChangeArgs(_serverState, prevState));
    }


    private void OnStopped()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Stopped;
        ServerStateChanged?.Invoke(new ServerStateChangeArgs(_serverState, prevState));
    }

#endregion


    public void Dispose()
    {
        StopServer(true);
    }
}