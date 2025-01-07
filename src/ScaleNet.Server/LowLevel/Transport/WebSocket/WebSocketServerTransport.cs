using System.Diagnostics;
using System.Net;
using ScaleNet.Common;
using ScaleNet.Common.LowLevel;
using ScaleNet.Server.LowLevel.Transport.WebSocket.Core;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket;

public sealed class WebSocketServerTransport : IServerTransport
{
    private readonly ServerSocket _serverSocket;
    private readonly WebSocketServerSettings _settings;

    public ushort Port => _settings.Port;
    public int MaxConnections => _settings.MaxConnections;
    public ServerState State => _serverSocket.State;
    
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    public event Action<ConnectionStateChangeArgs>? RemoteConnectionStateChanged;
    public event Action<ConnectionId, DeserializedNetMessage>? MessageReceived;


    public WebSocketServerTransport(WebSocketServerSettings settings)
    {
        _settings = settings;

        _serverSocket = new ServerSocket(settings.MaxFragmentSize, settings.SslContext);
        _serverSocket.ServerStateChanged += OnServerStateChanged;
        _serverSocket.RemoteConnectionStateChanged += OnRemoteConnectionStateChanged;
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


    private void OnRemoteConnectionStateChanged(ConnectionStateChangeArgs args)
    {
        try
        {
            RemoteConnectionStateChanged?.Invoke(args);
        }
        catch (Exception e)
        {
            ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(RemoteConnectionStateChanged)} event:\n{e}");
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
            ScaleNetManager.Logger.LogWarning($"Connection {connectionId} sent a packet that is too large. Kicking immediately.");
            StopConnection(connectionId, InternalDisconnectReason.OversizedPacket);
            return;
        }
        
        NetMessagePacket packet = NetMessagePacket.CreateIncomingNoCopy(data, false);
        
        bool serializeSuccess = NetMessages.TryDeserialize(packet, out DeserializedNetMessage msg);
                
        if (!serializeSuccess)
        {
            ScaleNetManager.Logger.LogWarning($"Received a packet that could not be deserialized. Kicking connection {connectionId} immediately.");
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

        bool started = _serverSocket.StartServer(_settings);
        
        if (started)
            ScaleNetManager.Logger.LogInfo("WS transport started successfully.");
        else
            ScaleNetManager.Logger.LogError("Failed to start WS transport.");
        
        return started;
    }


    public bool StopServer()
    {
        ScaleNetManager.Logger.LogInfo("Stopping WS transport...");
        
        foreach (ConnectionId conn in _serverSocket.ConnectedClients)
            StopConnection(conn, InternalDisconnectReason.ServerShutdown);
        
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
        Debug.Assert(connectionId != ConnectionId.Invalid, "Invalid connectionId.");
        
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
        
        return StopRemoteConnection(connectionId, true);
    }

    public bool StopConnectionImmediate(ConnectionId connectionId)
    {
        return StopRemoteConnection(connectionId, false);
    }


    private bool StopRemoteConnection(ConnectionId connectionId, bool iterateOutgoing)
    {
        bool success = _serverSocket.StopConnection(connectionId, iterateOutgoing);
        
        if (!success)
            ScaleNetManager.Logger.LogError($"Failed to disconnect connection {connectionId}.");
        
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