﻿using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ScaleNet.Common;
using ScaleNet.Common.LowLevel;

namespace ScaleNet.Server.LowLevel.Transport.Tcp;

public sealed class TcpServerTransport : SslServer, IServerTransport
{
    private readonly ConcurrentBag<uint> _availableSessionIds = [];
    private readonly ConcurrentDictionary<SessionId, TcpClientSession> _sessions = new();

    private bool _rejectNewConnections;
    private bool _rejectNewMessages;

    public readonly IPacketMiddleware? Middleware;
    public int MaxConnections { get; }
    public ServerState State { get; private set; } = ServerState.Stopped;

    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    public event Action<SessionStateChangeArgs>? SessionStateChanged;
    public event Action<SessionId, DeserializedNetMessage>? MessageReceived;


    public TcpServerTransport(ServerSslContext sslContext, IPAddress address, int port, int maxConnections, IPacketMiddleware? middleware = null) : base(sslContext, address, port)
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
                DisconnectSession(session, InternalDisconnectReason.ServerShutdown);
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
            while (session.IncomingPackets.TryDequeue(out NetMessagePacket packet))
            {
                bool serializeSuccess = NetMessages.TryDeserialize(packet, out DeserializedNetMessage msg);
                packet.Dispose();
                
                if (!serializeSuccess)
                {
                    ScaleNetManager.Logger.LogWarning($"Received a packet that could not be deserialized. Kicking session {session.SessionId} immediately.");
                    DisconnectSession(session, InternalDisconnectReason.MalformedData);
                    return;
                }

                try
                {
                    MessageReceived?.Invoke(session.SessionId, msg);
                }
                catch (Exception e)
                {
                    ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(MessageReceived)} event:\n{e}");
                    throw;
                }
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
        Debug.Assert(sessionId != SessionId.Invalid, "Invalid session ID.");

        if (sessionId == SessionId.Broadcast)
        {
            foreach (TcpClientSession session in _sessions.Values)
                QueueSendAsync(session, message);
        }
        else
        {
            if (!_sessions.TryGetValue(sessionId, out TcpClientSession? session))
            {
                ScaleNetManager.Logger.LogWarning($"Tried to send a packet to a non-existent/disconnected session with ID {sessionId}");
                return;
            }

            QueueSendAsync(session, message);
        }
    }


    private static void QueueSendAsync<T>(TcpClientSession session, T message) where T : INetMessage
    {
        // Write to a packet.
        if (!NetMessages.TrySerialize(message, out NetMessagePacket packet))
            return;
        
        session.OutgoingPackets.Enqueue(packet);
    }
    
    
    public ConnectionState GetConnectionState(SessionId sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out TcpClientSession? session))
            return session.ConnectionState;
        
        return ConnectionState.Disconnected;
    }


    public void DisconnectSession(SessionId sessionId, InternalDisconnectReason reason, bool iterateOutgoing = true)
    {
        if (!_sessions.TryGetValue(sessionId, out TcpClientSession? session))
        {
            ScaleNetManager.Logger.LogWarning($"Tried to disconnect a non-existent/disconnected session with ID {sessionId}");
            return;
        }
        
        DisconnectSession(session, reason, iterateOutgoing);
    }


    internal void DisconnectSession(TcpClientSession session, InternalDisconnectReason reason, bool iterateOutgoing = true)
    {
        if (iterateOutgoing)
        {
            QueueSendAsync(session, new InternalDisconnectMessage(reason));
            SendOutgoingPackets(session);
        }
        
        session.Disconnect();
    }


    private void SendOutgoingPackets(TcpClientSession session)
    {
        while (session.OutgoingPackets.TryDequeue(out NetMessagePacket packet))
        {
            Middleware?.HandleOutgoingPacket(ref packet);
            
            // Get a pooled buffer to add the length prefix.
            int payloadLength = packet.Length;
            int packetLength = payloadLength + 2;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(packetLength);
            
            // Add the 16-bit packet length prefix.
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, (ushort)payloadLength);
            
            // Copy the message data to the buffer.
            packet.AsSpan().CopyTo(buffer.AsSpan(2));
        
            session.SendAsync(buffer, 0, packetLength);
        
            // Return the buffer to the pool.
            ArrayPool<byte>.Shared.Return(buffer);
            
            packet.Dispose();
        }
    }


#region Session Lifetime

    protected override SslSession CreateSession()
    {
        bool isIdAvailable = _availableSessionIds.TryTake(out uint uId);
        
        if (!isIdAvailable)
            throw new InvalidOperationException("No available session IDs.");
        
        SessionId id = new(uId);

        TcpClientSession session = new(id, this, SessionStateChanged);
        _sessions.TryAdd(id, session);
        
        return session;
    }


    protected override bool AcceptClient(Socket client)
    {
        if (_rejectNewConnections || _sessions.Count >= MaxConnections)
            return false;
        
        return base.AcceptClient(client);
    }
    
    
    internal void ReleaseSession(SessionId id)
    {
        if (!_sessions.TryRemove(id, out TcpClientSession? session))
            return;
        
        _availableSessionIds.Add(id.Value);
        session.Dispose();
    }

#endregion


#region Server Lifetime

    protected override void OnStarting()
    {
        OnServerStateChanged(ServerState.Starting);
    }


    protected override void OnStarted()
    {
        OnServerStateChanged(ServerState.Started);
    }

    
    protected override void OnStopping()
    {
        OnServerStateChanged(ServerState.Stopping);
    }


    protected override void OnStopped()
    {
        OnServerStateChanged(ServerState.Stopped);
    }
    
    
    private void OnServerStateChanged(ServerState newState)
    {
        ServerState prevState = State;
        State = newState;
        try
        {
            ServerStateChanged?.Invoke(new ServerStateChangeArgs(State, prevState));
        }
        catch (Exception e)
        {
            ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(ServerStateChanged)} event:\n{e}");
            throw;
        }
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