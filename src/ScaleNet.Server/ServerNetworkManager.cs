using ScaleNet.Common;
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


    public ServerNetworkManager(IServerTransport transport, ConnectionManager<TConnection> connectionManager)
    {
        if(!ScaleNetManager.IsInitialized)
            throw new InvalidOperationException("Networking.Initialize() must be called before creating a server.");

        _transport = transport;
        ConnectionManager = connectionManager;
        _messageHandlerManager = new MessageHandlerManager<TConnection>();
        
        _transport.ServerStateChanged += OnServerStateChanged;
        _transport.SessionStateChanged += OnSessionStateChanged;
        _transport.MessageReceived += OnMessageReceived;
    }

    
    public void Start()
    {
        _transport.StartServer();
    }


    public void Stop(bool gracefully = true)
    {
        _transport.StopServer(gracefully);
    }

    
    public void Update()
    {
        _transport.HandleIncomingMessages();
        _transport.HandleOutgoingMessages();
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
    
    private void OnMessageReceived(SessionId sessionId, DeserializedNetMessage msg)
    {
        if (!ConnectionManager.TryGetConnection(sessionId, out TConnection? connection))
        {
            ScaleNetManager.Logger.LogWarning($"Received a message from an unknown session {sessionId}. Ignoring, and ending the session.");
            _transport.DisconnectSession(sessionId, InternalDisconnectReason.UnexpectedProblem);
            return;
        }
        
        ScaleNetManager.Logger.LogDebug($"RCV - {msg.Type} from session {connection.SessionId}");
        
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

    private void OnSessionStateChanged(SessionStateChangeArgs sessionStateChangeArgs)
    {
        SessionId sessionId = sessionStateChangeArgs.SessionId;
        TConnection? connection;
        
        ScaleNetManager.Logger.LogInfo($"Session {sessionId} is {sessionStateChangeArgs.NewState.ToString().ToLower()}");
        
        switch (sessionStateChangeArgs.NewState)
        {
            case ConnectionState.Connecting:
            {
                if (!ConnectionManager.TryCreateConnection(sessionId, out connection))
                {
                    ScaleNetManager.Logger.LogWarning($"Client for session {sessionId} already exists. Kicking.");
                    _transport.DisconnectSession(sessionId, InternalDisconnectReason.UnexpectedProblem);
                    return;
                }
                
                break;
            }
            case ConnectionState.Disconnected:
            {
                if (!ConnectionManager.TryRemoveConnection(sessionId, out connection))
                {
                    ScaleNetManager.Logger.LogWarning($"Client for session {sessionId} not found in the client manager.");
                    return;
                }
                
                break;
            }
            case ConnectionState.Ready:
            case ConnectionState.Connected:
            case ConnectionState.Disconnecting:
            {
                if (!ConnectionManager.TryGetConnection(sessionId, out connection))
                {
                    ScaleNetManager.Logger.LogWarning($"Client for session {sessionId} not found in the client manager.");
                    return;
                }

                break;
            }
            default:
                throw new InvalidOperationException($"Unknown session state: {sessionStateChangeArgs.NewState}");
        }
                
        ClientStateChanged?.Invoke(new ClientStateChangeArgs<TConnection>(connection, sessionStateChangeArgs.NewState));
    }

#endregion


    public void Dispose()
    {
        _transport.Dispose();
    }
}