using System.Diagnostics;
using System.Net;
using ScaleNet.Common;
using ScaleNet.Common.LowLevel;
using ScaleNet.Server.LowLevel.Transport.WebSocket.Core;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket;

public sealed class WebSocketServerTransport : IServerTransport
{
    /// <summary>
    /// Minimum MTU allowed.
    /// </summary>
    private const int MINIMUM_MTU = 576;
    
    /// <summary>
    /// Maximum MTU allowed.
    /// </summary>
    private const int MAXIMUM_MTU = ushort.MaxValue;
    
    private readonly ServerSocket _serverSocket;

    public ushort Port { get; }
    public int MaxConnections { get; }
    public ServerState State => _serverSocket.State;
    
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    public event Action<SessionStateChangeArgs>? SessionStateChanged;
    public event Action<ConnectionId, DeserializedNetMessage>? MessageReceived;


    public WebSocketServerTransport(ServerSslContext sslContext, ushort port, int maxConnections, int maxPacketSize = SharedConstants.MAX_PACKET_SIZE_BYTES)
    {
        Port = port;
        MaxConnections = maxConnections;
        
        if (maxPacketSize < 0)
            maxPacketSize = MINIMUM_MTU;
        else if (maxPacketSize > MAXIMUM_MTU)
            maxPacketSize = MAXIMUM_MTU;

        _serverSocket = new ServerSocket(maxPacketSize, sslContext);
        _serverSocket.ServerStateChanged += OnServerStateChanged;
        _serverSocket.SessionStateChanged += OnSessionStateChanged;
        _serverSocket.DataReceived += OnReceivedData;
    }


    public void Dispose()
    {
        StopServer();
        
        _serverSocket.Dispose();
    }


    private void OnServerStateChanged(ServerStateChangeArgs args)
    {
        try
        {
            ServerStateChanged?.Invoke(args);
        }
        catch (Exception e)
        {
            ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(ServerStateChanged)} event:\n{e}");
            throw;
        }
    }


    private void OnSessionStateChanged(SessionStateChangeArgs args)
    {
        try
        {
            SessionStateChanged?.Invoke(args);
        }
        catch (Exception e)
        {
            ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(SessionStateChanged)} event:\n{e}");
            throw;
        }
    }


    private void OnReceivedData(ConnectionId connectionId, ArraySegment<byte> data)
    {
        /*
         TODO: Implement too-many-packets check for WS transport
         if (IncomingPackets.Count > ServerConstants.MAX_PACKETS_PER_TICK)
        {
            ScaleNetManager.Logger.LogWarning($"Session {connectionId} is sending too many packets. Kicking immediately.");
            StopConnection(connectionId, InternalDisconnectReason.TooManyPackets);
            return;
        }*/
        
        if (data.Count > SharedConstants.MAX_MESSAGE_SIZE_BYTES)
        {
            ScaleNetManager.Logger.LogWarning($"Session {connectionId} sent a packet that is too large. Kicking immediately.");
            StopConnection(connectionId, InternalDisconnectReason.OversizedPacket);
            return;
        }
        
        NetMessagePacket packet = NetMessagePacket.CreateIncomingNoCopy(data, false);
        
        bool serializeSuccess = NetMessages.TryDeserialize(packet, out DeserializedNetMessage msg);
                
        if (!serializeSuccess)
        {
            ScaleNetManager.Logger.LogWarning($"Received a packet that could not be deserialized. Kicking connectionId {connectionId} immediately.");
            StopConnection(connectionId, InternalDisconnectReason.MalformedData);
            return;
        }

        try
        {
            MessageReceived?.Invoke(connectionId, msg);
        }
        catch (Exception e)
        {
            ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(MessageReceived)} event:\n{e}");
            throw;
        }
    }


#region Starting and stopping

    public bool StartServer()
    {
        ScaleNetManager.Logger.LogInfo($"Starting WS transport on port {Port}...");

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
        
        foreach (ConnectionId session in _serverSocket.ConnectedClients)
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

    public void QueueSendAsync<T>(ConnectionId connectionId, T message) where T : INetMessage
    {
        Debug.Assert(connectionId != ConnectionId.Invalid, "Invalid connectionId ID.");
        
        // Write to a packet.
        if (!NetMessages.TrySerialize(message, out NetMessagePacket packet))
            return;

        // _serverSocket internally handles the broadcast ID.
        _serverSocket.QueueSend(connectionId, packet);
    }

#endregion


#region Utils

    public bool StopConnection(ConnectionId connectionId, InternalDisconnectReason reason)
    {
        QueueSendAsync(connectionId, new InternalDisconnectMessage(reason));
        
        return DisconnectSession(connectionId, true);
    }

    public bool StopConnectionImmediate(ConnectionId connectionId)
    {
        return DisconnectSession(connectionId, false);
    }


    private bool DisconnectSession(ConnectionId connectionId, bool iterateOutgoing)
    {
        bool success = _serverSocket.StopConnection(connectionId, iterateOutgoing);
        
        if (!success)
            ScaleNetManager.Logger.LogError($"Failed to disconnect connectionId {connectionId}.");
        
        return success;
    }


    public ConnectionState GetConnectionState(ConnectionId connectionId)
    {
        return _serverSocket.GetConnectionState(connectionId);
    }
    
    
    public EndPoint? GetClientEndPoint(ConnectionId connectionId)
    {
        return _serverSocket.GetConnectionEndPoint(connectionId);
    }

#endregion
}