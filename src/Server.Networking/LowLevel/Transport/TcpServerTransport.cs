using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using Server.Networking.HighLevel;
using Shared.Networking;
using Shared.Utils;

namespace Server.Networking.LowLevel.Transport;

internal class TcpClientSession(SessionId id, TcpServerTransport transport, IPacketMiddleware? middleware) : TcpSession(transport)
{
    private readonly IPacketMiddleware? _middleware = middleware;
    public readonly SessionId SessionId = id;
    
    // Packets need to be stored per-session, to, for example, allow sending all queued packets before disconnecting.
    public readonly ConcurrentQueue<Packet> OutgoingPackets = new();
    public readonly ConcurrentQueue<Packet> IncomingPackets = new();


    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        ReadOnlyMemory<byte> memory = new(buffer, (int)offset, (int)size);
        
        _middleware?.HandleIncomingPacket(ref memory);
        
        Packet packet = new(SessionId, memory);
        IncomingPackets.Enqueue(packet);
    }


    protected override void OnError(SocketError error)
    {
        Logger.LogError($"TCP session with Id {Id} caught an error: {error}");
    }
}

public class TcpServerTransport : TcpServer, IServerTransport
{
    private readonly ConcurrentBag<uint> _availableSessionIds = [];
    private readonly ConcurrentDictionary<SessionId, TcpClientSession> _sessions = new();
    private readonly IPacketMiddleware? _middleware;
    
    private ServerState _serverState = ServerState.Stopped;

    public int MaxConnections { get; }
    public bool RejectNewConnections { get; set; }
    public bool RejectNewPackets { get; set; }
    
    public event Action<ServerStateArgs>? ServerStateChanged;
    public event Action<SessionStateArgs>? SessionStateChanged;
    public event Action<Packet>? HandlePacket;


    public TcpServerTransport(IPAddress address, int port, int maxConnections, IPacketMiddleware? middleware = null) : base(address, port)
    {
        MaxConnections = maxConnections;
        _middleware = middleware;

        // Fill the available session IDs bag.
        for (uint i = 1; i < uint.MaxValue; i++)
            _availableSessionIds.Add(i);
    }
    
    
    public void HandleIncomingPackets()
    {
        //TODO: Parallelize.
        foreach (TcpClientSession session in _sessions.Values)
        {
            while (session.IncomingPackets.TryDequeue(out Packet packet))
                HandlePacket?.Invoke(packet);
        }
    }
    
    
    public void HandleOutgoingPackets()
    {
        //TODO: Parallelize.
        // Sessions that are iterated first have packet priority.
        foreach (TcpClientSession session in _sessions.Values)
        {
            while (session.OutgoingPackets.TryDequeue(out Packet packet))
            {
                ReadOnlyMemory<byte> buffer = packet.Data;
                
                _middleware?.HandleOutgoingPacket(ref buffer);

                session.SendAsync(buffer.Span);
            }
        }
    }

    
    public void QueueSendAsync(Packet packet)
    {
        SessionId sessionId = packet.SessionId;
        
        if (!_sessions.TryGetValue(sessionId, out TcpClientSession? session))
        {
            Logger.LogWarning($"Tried to send a packet to a non-existent/disconnected session with ID {sessionId}");
            return;
        }
        
        session.OutgoingPackets.Enqueue(packet);
    }


#region Session Lifetime

    protected override TcpSession CreateSession()
    {
        bool isIdAvailable = _availableSessionIds.TryTake(out uint uId);
        
        if (!isIdAvailable)
            throw new InvalidOperationException("No available session IDs.");
        
        SessionId id = new(uId);

        TcpClientSession session = new(id, this, _middleware);
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
        
        SessionStateChanged?.Invoke(new SessionStateArgs(id, SessionState.Connecting));
    }
    

    protected override void OnConnected(TcpSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateArgs(id, SessionState.Connected));
        
        //TODO: Figure out an way to reject new connections earlier.
        if (RejectNewConnections || _sessions.Count >= MaxConnections)
            session.Disconnect();
    }


    protected override void OnDisconnecting(TcpSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateArgs(id, SessionState.Disconnecting));
    }


    protected override void OnDisconnected(TcpSession session)
    {
        SessionId id = ((TcpClientSession)session).SessionId;
        
        SessionStateChanged?.Invoke(new SessionStateArgs(id, SessionState.Disconnected));
        
        OnEndSession(id);
    }

#endregion


#region Server Lifetime

    protected override void OnStarting()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Starting;
        ServerStateChanged?.Invoke(new ServerStateArgs(_serverState, prevState));
    }


    protected override void OnStarted()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Started;
        ServerStateChanged?.Invoke(new ServerStateArgs(_serverState, prevState));
    }

    
    protected override void OnStopping()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Stopping;
        ServerStateChanged?.Invoke(new ServerStateArgs(_serverState, prevState));
    }


    protected override void OnStopped()
    {
        ServerState prevState = _serverState;
        _serverState = ServerState.Stopped;
        ServerStateChanged?.Invoke(new ServerStateArgs(_serverState, prevState));
    }

#endregion


    protected override void OnError(SocketError error)
    {
        Logger.LogError($"Chat TCP server caught an error with code {error}");
    }
}