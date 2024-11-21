using ScaleNet.Common;
using ScaleNet.Server.LowLevel;
using ScaleNet.Server.LowLevel.Transport;

namespace ScaleNet.Server;

public sealed class NetServer : IDisposable
{
    private readonly MessageHandlerManager _messageHandlerManager;
    private readonly ClientManager _clientManager;

    public readonly IServerTransport Transport;
    
    /// <summary>
    /// True if the server is started and listening for incoming connections.
    /// </summary>
    public bool IsStarted { get; private set; }

    /// <summary>
    /// Called after the server state changes.
    /// </summary>
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    
    /// <summary>
    /// Called after a client's state changes.
    /// </summary>
    public event Action<ClientStateChangeArgs>? ClientStateChanged;


    public NetServer(IServerTransport transport)
    {
        if(!Networking.IsInitialized)
            throw new InvalidOperationException("Networking.Initialize() must be called before creating a server.");

        Transport = transport;
        _messageHandlerManager = new MessageHandlerManager();
        _clientManager = new ClientManager(this);
        
        Transport.ServerStateChanged += OnServerStateChanged;
        Transport.SessionStateChanged += OnSessionStateChanged;
        Transport.MessageReceived += OnMessageReceived;
    }

    
    public void Start()
    {
        Transport.StartServer();
    }


    public void Stop(bool gracefully = true)
    {
        Transport.StopServer(gracefully);
    }

    public void Update()
    {
        Transport.HandleIncomingMessages();
        Transport.HandleOutgoingMessages();
    }
    

    /// <summary>
    /// Registers a method to call when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">Method to call.</param>
    /// <typeparam name="T"></typeparam>
    /// <param name="requiresAuthentication">True if the client must be authenticated to send this message.</param>
    public void RegisterMessageHandler<T>(Action<Client, T> handler, bool requiresAuthentication = true) where T : INetMessage => _messageHandlerManager.RegisterMessageHandler(handler, requiresAuthentication);


    /// <summary>
    /// Unregisters a method from being called when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">The method to unregister.</param>
    /// <typeparam name="T">Type of message to unregister.</typeparam>
    public void UnregisterMessageHandler<T>(Action<Client, T> handler) where T : INetMessage => _messageHandlerManager.UnregisterMessageHandler(handler);


#region Sending messages

    public void SendMessageToClient<T>(Client client, T message, bool requireAuthenticated = true) where T : INetMessage
    {
        if (!IsStarted)
        {
            Networking.Logger.LogWarning("Cannot send message to client because server is not active.");
            return;
        }

        if (requireAuthenticated && !client.IsAuthenticated)
        {
            Networking.Logger.LogWarning($"Cannot send message {message} to client {client.SessionId} because they are not authenticated.");
            return;
        }

        client.QueueSend(message);
    }


    /// <summary>
    /// Sends a message to all clients.
    /// </summary>
    /// <param name="message">Message to send.</param>
    /// <param name="requireAuthenticated">True if the client must be authenticated to receive this message.</param>
    /// <typeparam name="T">The type of message to send.</typeparam>
    public void SendMessageToAllClients<T>(T message, bool requireAuthenticated = true) where T : INetMessage
    {
        if (!IsStarted)
        {
            Networking.Logger.LogWarning("Cannot send message to clients because server is not active.");
            return;
        }

        foreach (Client c in _clientManager.Clients)
        {
            if (requireAuthenticated && !c.IsAuthenticated)
                continue;
            
            SendMessageToClient(c, message, requireAuthenticated);
        }
    }


    /// <summary>
    /// Sends a message to all clients except the specified one.
    /// </summary>
    public void SendMessageToAllClientsExcept<T>(T message, Client except, bool requireAuthenticated = true) where T : INetMessage
    {
        if (!IsStarted)
        {
            Networking.Logger.LogWarning("Cannot send message to clients because server is not active.");
            return;
        }

        foreach (Client c in _clientManager.Clients)
        {
            if (c == except)
                continue;
            
            if (requireAuthenticated && !c.IsAuthenticated)
                continue;
            
            SendMessageToClient(c, message, requireAuthenticated);
        }
    }


    /// <summary>
    /// Sends a message to all clients except the specified ones.
    /// </summary>
    public void SendMessageToAllClientsExcept<T>(T message, List<Client> except, bool requireAuthenticated = true) where T : INetMessage
    {
        if (!IsStarted)
        {
            Networking.Logger.LogWarning("Cannot send message to clients because server is not active.");
            return;
        }

        foreach (Client c in _clientManager.Clients)
        {
            if (except.Contains(c))
                continue;
            
            if (requireAuthenticated && !c.IsAuthenticated)
                continue;

            SendMessageToClient(c, message, requireAuthenticated);
        }
    }

#endregion


#region Message processing
    
    private void OnMessageReceived(SessionId sessionId, DeserializedNetMessage msg)
    {
        if (!_clientManager.TryGetClient(sessionId, out Client? client))
        {
            Networking.Logger.LogWarning($"Received a message from an unknown session {sessionId}. Ignoring.");
            return;
        }
        
        Networking.Logger.LogDebug($"RCV - {msg.Type} from session {client.SessionId}");
        
        _messageHandlerManager.TryHandleMessage(client, msg);
    }

#endregion


#region Server state

    private void OnServerStateChanged(ServerStateChangeArgs args)
    {
        ServerState state = args.NewState;
        IsStarted = state == ServerState.Started;

        Networking.Logger.LogInfo($"Server is {state.ToString().ToLower()}");

        ServerStateChanged?.Invoke(args);
    }

#endregion


#region Client state

    private void OnSessionStateChanged(SessionStateChangeArgs sessionStateChangeArgs)
    {
        SessionId sessionId = sessionStateChangeArgs.SessionId;
        Client? client;
        
        Networking.Logger.LogInfo($"Session {sessionId} is {sessionStateChangeArgs.NewState.ToString().ToLower()}");
        
        switch (sessionStateChangeArgs.NewState)
        {
            case ConnectionState.Connecting:
            {
                if (!_clientManager.TryAddClient(sessionId, out client))
                {
                    Networking.Logger.LogWarning($"Client for session {sessionId} already exists. Kicking.");
                    Transport.DisconnectSession(sessionId, DisconnectReason.UnexpectedProblem);
                    return;
                }
                
                break;
            }
            case ConnectionState.Disconnected:
            {
                if (!_clientManager.TryRemoveClient(sessionId, out client))
                {
                    Networking.Logger.LogWarning($"Client for session {sessionId} not found in the client manager.");
                    return;
                }
                
                break;
            }
            case ConnectionState.SslHandshaking:
            case ConnectionState.SslHandshaked:
            case ConnectionState.Connected:
            case ConnectionState.Disconnecting:
            {
                if (!_clientManager.TryGetClient(sessionId, out client))
                {
                    Networking.Logger.LogWarning($"Client for session {sessionId} not found in the client manager.");
                    return;
                }

                break;
            }
            default:
                throw new InvalidOperationException($"Unknown session state: {sessionStateChangeArgs.NewState}");
        }
                
        ClientStateChanged?.Invoke(new ClientStateChangeArgs(client, sessionStateChangeArgs.NewState));
    }

#endregion


    public void Dispose()
    {
        Transport.Dispose();
    }
}