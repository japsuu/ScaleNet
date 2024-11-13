using System.Collections.Concurrent;
using Server.Networking.Authentication;
using Server.Networking.HighLevel;
using Server.Networking.LowLevel;
using Server.Networking.LowLevel.Transport;
using Shared;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Server.Networking;

public class NetServer
{
    private readonly MessageHandlerManager _messageHandlerManager;
    private readonly Authenticator? _authenticator;
    private readonly ClientManager _clientManager;
    
    public readonly IServerTransport Transport;
    
    /// <summary>
    /// True if the server is started and listening for incoming connections.
    /// </summary>
    public bool IsStarted { get; private set; }

    /// <summary>
    /// Called after the server state changes.
    /// </summary>
    public event Action<ServerStateArgs>? ServerStateChanged;
    
    /// <summary>
    /// Called after a client's state changes.
    /// </summary>
    public event Action<ClientStateArgs>? ClientStateChanged;


    public NetServer(IServerTransport transport)
    {
        Transport = transport;
        _messageHandlerManager = new MessageHandlerManager();
        _clientManager = new ClientManager(this);
        
        _authenticator = new Authenticator(this, new DefaultAuthenticationResolver(SharedConstants.DEVELOPMENT_AUTH_PASSWORD));
        _authenticator.ClientAuthSuccess += OnClientAuthenticated;
        
        Transport.ServerStateChanged += OnServerStateChanged;
        Transport.SessionStateChanged += OnSessionStateChanged;
        Transport.HandleMessage += OnMessageReceived;
    }

    
    public void Start()
    {
        Transport.Start();
    }


    public void Stop(bool gracefully = true)
    {
        if (gracefully)
        {
            Transport.RejectNewConnections = true;
            Transport.RejectNewMessages = true;

            foreach (Client session in _clientManager.Clients)
            {
                session.Kick(DisconnectReason.ServerShutdown);
            }
        }
        
        Transport.Stop();
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
            Logger.LogWarning("Cannot send message to client because server is not active.");
            return;
        }

        if (requireAuthenticated && !client.IsAuthenticated)
        {
            Logger.LogWarning($"Cannot send message {message} to client {client.SessionId} because they are not authenticated.");
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
            Logger.LogWarning("Cannot send message to clients because server is not active.");
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
            Logger.LogWarning("Cannot send message to clients because server is not active.");
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
            Logger.LogWarning("Cannot send message to clients because server is not active.");
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
    
    private void OnMessageReceived(SessionId sessionId, INetMessage msg)
    {
        if (!_clientManager.TryGetClient(sessionId, out Client? client))
        {
            Logger.LogWarning($"Received a message from an unknown client {sessionId}. Ignoring.");
            return;
        }
        
        Type messageId = msg.GetType();
        
        Logger.LogDebug($"Received message {messageId} from client {client.SessionId}.");
        
        _messageHandlerManager.TryHandleMessage(client, msg);
    }

#endregion


#region Server state

    private void OnServerStateChanged(ServerStateArgs args)
    {
        ServerState state = args.NewState;
        IsStarted = state == ServerState.Started;

        Logger.LogInfo($"Server is {state.ToString().ToLower()}");

        ServerStateChanged?.Invoke(args);
    }

#endregion


#region Client state

    private void OnSessionStateChanged(SessionStateArgs sessionStateArgs)
    {
        SessionId sessionId = sessionStateArgs.SessionId;
        Client? client;
        
        Logger.LogInfo($"Session {sessionId} is {sessionStateArgs.State.ToString().ToLower()}");
        
        switch (sessionStateArgs.State)
        {
            case SessionState.Connecting:
            {
                if (!_clientManager.TryAddClient(sessionId, out client))
                {
                    Logger.LogWarning($"Client for session {sessionId} already exists. Kicking.");
                    Transport.DisconnectSession(sessionId, DisconnectReason.DuplicateSession);
                    return;
                }
                
                if (_authenticator != null)
                    _authenticator.OnNewClientConnected(client);
                else
                    OnClientAuthenticated(client);
                
                break;
            }
            case SessionState.Connected:
            {
                if (!_clientManager.TryGetClient(sessionId, out client))
                {
                    Logger.LogWarning($"Client for session {sessionId} not found in the client manager.");
                    return;
                }
                
                break;
            }
            case SessionState.Disconnecting:
            {
                if (!_clientManager.TryGetClient(sessionId, out client))
                {
                    Logger.LogWarning($"Client for session {sessionId} not found in the client manager.");
                    return;
                }
                
                break;
            }
            case SessionState.Disconnected:
            {
                if (!_clientManager.TryRemoveClient(sessionId, out client))
                {
                    Logger.LogWarning($"Client for session {sessionId} not found in the client manager.");
                    return;
                }
                
                break;
            }
            default:
                throw new InvalidOperationException($"Unknown session state {sessionStateArgs.State}");
        }
                
        ClientStateChanged?.Invoke(new ClientStateArgs(client, sessionStateArgs.State));
    }

#endregion


#region Authentication

    /// <summary>
    /// Called when a remote client authenticates with the server.
    /// </summary>
    private void OnClientAuthenticated(Client session)
    {
        Logger.LogInfo($"Client {session.SessionId} authenticated!");
        
        // Send the client a welcome message.
        session.QueueSend(new WelcomeMessage(session.SessionId.Value));
        
        // Load user data.
        if (!session.LoadPlayerData())
        {
            Logger.LogWarning($"Client {session.SessionId} player data could not be loaded.");
            session.Kick(DisconnectReason.CorruptPlayerData);
            return;
        }
        
        SendMessageToAllClientsExcept(new ChatMessageNotification(session.PlayerData!.Username, "Joined the chat."), session);
    }

#endregion
}