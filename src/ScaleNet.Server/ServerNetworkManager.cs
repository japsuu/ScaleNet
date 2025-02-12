﻿using ScaleNet.Common;
using ScaleNet.Common.LowLevel;
using ScaleNet.Server.LowLevel;
using ScaleNet.Server.LowLevel.Transport;

namespace ScaleNet.Server;

public sealed class ServerNetworkManager<TConnection> : IDisposable where TConnection : Connection
{
    private readonly MessageHandlerManager<TConnection> _messageHandlerManager;
    private readonly IServerTransport _transport;

    public readonly ConnectionManager<TConnection> ConnectionManager;
    
    /// <summary>
    /// True if the server is started and listening for incoming connections.
    /// </summary>
    public bool IsStarted { get; private set; }
    
    /// <returns>All connections.</returns>
    public IEnumerable<TConnection> Connections => ConnectionManager.Connections;
    
    public int ConnectionCount => ConnectionManager.ConnectionCount;
    
    public int MaxConnections => _transport.MaxConnections;

    /// <summary>
    /// Called after the server state changes.
    /// </summary>
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    
    /// <summary>
    /// Called after a client's state changes.
    /// </summary>
    public event Action<ClientStateChangeArgs<TConnection>>? ClientStateChanged;


    /// <summary>
    /// Creates a new server network manager.
    /// </summary>
    /// <param name="transport">The transport to use for the server.</param>
    /// <param name="connectionManager">The connection manager to use for the server.</param>
    /// <exception cref="InvalidOperationException">Thrown if ScaleNetManager.Initialize() has not been called.</exception>
    public ServerNetworkManager(IServerTransport transport, ConnectionManager<TConnection> connectionManager)
    {
        if(!ScaleNetManager.IsInitialized)
            throw new InvalidOperationException("Networking.Initialize() must be called before creating a server.");

        _transport = transport;
        ConnectionManager = connectionManager;
        _messageHandlerManager = new MessageHandlerManager<TConnection>();
        
        _transport.ServerStateChanged += OnServerStateChanged;
        _transport.RemoteConnectionStateChanged += OnRemoteConnectionStateChanged;
        _transport.MessageReceived += OnMessageReceived;
        
        RegisterMessageHandler<InternalPingMessage>(OnPingMessageReceived);
        RegisterMessageHandler<InternalPongMessage>(OnPongMessageReceived);
    }


    public void Start()
    {
        _transport.StartServer();
    }


    public void Stop()
    {
        _transport.StopServer();
    }

    
    public void Update()
    {
        _transport.IterateIncomingMessages();
        
        ConnectionManager.PingConnections();
        
        _transport.IterateOutgoingMessages();
    }


    /// <summary>
    /// Registers a method to call when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">Method to call.</param>
    /// <typeparam name="T"></typeparam>
    public void RegisterMessageHandler<T>(Action<TConnection, T> handler) where T : INetMessage => _messageHandlerManager.RegisterMessageHandler(handler);


    /// <summary>
    /// Unregisters a method from being called when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">The method to unregister.</param>
    /// <typeparam name="T">Type of message to unregister.</typeparam>
    public void UnregisterMessageHandler<T>(Action<TConnection, T> handler) where T : INetMessage => _messageHandlerManager.UnregisterMessageHandler(handler);


#region Sending messages

    public void SendMessageToClient<T>(Connection connection, T message) where T : INetMessage
    {
        if (!IsStarted)
        {
            ScaleNetManager.Logger.LogWarning("Cannot send message to client because server is not active.");
            return;
        }

        connection.QueueSend(message);
    }


    /// <summary>
    /// Sends a message to all clients.
    /// </summary>
    /// <param name="message">Message to send.</param>
    /// <param name="precondition">Optional precondition to check per client before sending the message.</param>
    /// <typeparam name="T">The type of message to send.</typeparam>
    public void SendMessageToAllClients<T>(T message, Func<TConnection, bool>? precondition = null) where T : INetMessage
    {
        if (!IsStarted)
        {
            ScaleNetManager.Logger.LogWarning("Cannot send message to clients because server is not active.");
            return;
        }

        foreach (TConnection c in ConnectionManager.Connections)
        {
            if (precondition != null && !precondition(c))
                continue;
            
            SendMessageToClient(c, message);
        }
    }


    /// <summary>
    /// Sends a message to all clients except the specified one.
    /// </summary>
    public void SendMessageToAllClientsExcept<T>(T message, TConnection except, Func<TConnection, bool>? precondition = null) where T : INetMessage
    {
        if (!IsStarted)
        {
            ScaleNetManager.Logger.LogWarning("Cannot send message to clients because server is not active.");
            return;
        }

        foreach (TConnection c in ConnectionManager.Connections)
        {
            if (c == except)
                continue;
            
            if (precondition != null && !precondition(c))
                continue;
            
            SendMessageToClient(c, message);
        }
    }


    /// <summary>
    /// Sends a message to all clients except the specified ones.
    /// </summary>
    public void SendMessageToAllClientsExcept<T>(T message, List<TConnection> except, Func<TConnection, bool>? precondition = null) where T : INetMessage
    {
        if (!IsStarted)
        {
            ScaleNetManager.Logger.LogWarning("Cannot send message to clients because server is not active.");
            return;
        }

        foreach (TConnection c in ConnectionManager.Connections)
        {
            if (except.Contains(c))
                continue;
            
            if (precondition != null && !precondition(c))
                continue;

            SendMessageToClient(c, message);
        }
    }

#endregion


#region Message processing
    
    private void OnMessageReceived(ConnectionId connectionId, DeserializedNetMessage msg)
    {
        if (!ConnectionManager.TryGetConnection(connectionId, out TConnection? connection))
        {
            ScaleNetManager.Logger.LogWarning($"Received a message from an unknown connectionId {connectionId}. Ignoring, and ending the connectionId.");
            _transport.StopConnection(connectionId, InternalDisconnectReason.UnexpectedProblem);
            return;
        }
        
        ScaleNetManager.Logger.LogDebug($"RCV - {msg.Type} from connectionId {connection.ConnectionId}");
        
        _messageHandlerManager.TryHandleMessage(connection, msg);
    }

#endregion


#region Server state

    private void OnServerStateChanged(ServerStateChangeArgs args)
    {
        ServerState state = args.NewState;
        IsStarted = state == ServerState.Started;

        ScaleNetManager.Logger.LogInfo($"Server is {state.ToString().ToLower()}");

        ServerStateChanged?.Invoke(args);
    }

#endregion


#region Client state

    private void OnRemoteConnectionStateChanged(ConnectionStateChangeArgs connectionStateChangeArgs)
    {
        ConnectionId connectionId = connectionStateChangeArgs.ConnectionId;
        TConnection? connection;
        
        ScaleNetManager.Logger.LogInfo($"Connection {connectionId} is {connectionStateChangeArgs.NewState.ToString().ToLower()}");
        
        switch (connectionStateChangeArgs.NewState)
        {
            case ConnectionState.Connected:
            {
                if (!ConnectionManager.TryCreateConnection(connectionId, out connection))
                {
                    ScaleNetManager.Logger.LogWarning($"Client for connection {connectionId} already exists. Kicking.");
                    _transport.StopConnection(connectionId, InternalDisconnectReason.UnexpectedProblem);
                    return;
                }
                
                break;
            }
            case ConnectionState.Disconnected:
            {
                if (!ConnectionManager.TryRemoveConnection(connectionId, out connection))
                {
                    ScaleNetManager.Logger.LogWarning($"Client for connection {connectionId} not found in the client manager.");
                    return;
                }
                
                break;
            }
            default:
                throw new InvalidOperationException($"Unknown connectionId state: {connectionStateChangeArgs.NewState}");
        }
                
        ClientStateChanged?.Invoke(new ClientStateChangeArgs<TConnection>(connection, connectionStateChangeArgs.NewState));
    }

#endregion


#region Pinging
    
    /// <summary>
    /// Called when a ping message is received from a client.
    /// </summary>
    private static void OnPingMessageReceived(TConnection connection, InternalPingMessage msg)
    {
        connection.OnPingReceived();
    }


    /// <summary>
    /// Called when a pong message is received from a client.
    /// </summary>
    private static void OnPongMessageReceived(TConnection connection, InternalPongMessage msg)
    {
        connection.OnPongReceived();
    }

#endregion


    public void Dispose()
    {
        _transport.Dispose();
    }
}