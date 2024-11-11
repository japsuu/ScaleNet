using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Server.Authentication;
using Server.Networking;
using Server.Networking.LowLevel;
using Shared;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Server;

internal class GameServer
{
    private readonly TcpGameServer _tcpServer;
    private readonly Authenticator? _authenticator;
    private readonly SessionManager _sessionManager;
    private readonly ConcurrentDictionary<Type, MessageHandler> _messageHandlers;
    
    /// <summary>
    /// True if the server is started and listening for incoming connections.
    /// </summary>
    public bool IsStarted { get; private set; }

    /// <summary>
    /// Called after the server state changes.
    /// </summary>
    public event Action<ServerStateArgs>? StateChanged;


    public GameServer(IPAddress address, int port)
    {
        _tcpServer = new TcpGameServer(address, port);
        _sessionManager = new SessionManager(this);
        _messageHandlers = new ConcurrentDictionary<Type, MessageHandler>();
        
        _authenticator = new Authenticator(this, SharedConstants.DEVELOPMENT_AUTH_PASSWORD);
        _authenticator.AuthenticationResultConcluded += OnAuthenticatorResultConcluded;
        
        _tcpServer.ServerStateChanged += OnServerStateChanged;
        _tcpServer.ClientStateChanged += OnClientStateChanged;
        
        RegisterMessageHandler<ChatMessage>(OnChatMessageReceived);
    }


#region Starting and stopping

    public void Start()
    {
        _tcpServer.Start();
    }


    public void Stop(bool gracefully = true)
    {
        if (gracefully)
        {
            _tcpServer.RejectNewConnections = true;
            _tcpServer.RejectNewPackets = true;

            foreach (PlayerSession session in _sessionManager.Sessions)
            {
                session.Kick(DisconnectReason.ServerShutdown);
            }
        }
        
        _tcpServer.Stop();
    }

#endregion


#region Received message handling

    /// <summary>
    /// Registers a method to call when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">Method to call.</param>
    /// <typeparam name="T"></typeparam>
    /// <param name="requiresAuthentication">True if the client must be authenticated to send this message.</param>
    public void RegisterMessageHandler<T>(Action<PlayerSession, T> handler, bool requiresAuthentication = true) where T : INetMessage
    {
        Type key = typeof(T);
        
        if (!_messageHandlers.TryGetValue(key, out MessageHandler? handlerCollection))
        {
            handlerCollection = new MessageHandler<T>(requiresAuthentication);
            _messageHandlers.TryAdd(key, handlerCollection);
        }

        handlerCollection.RegisterAction(handler);
    }


    /// <summary>
    /// Unregisters a method from being called when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">The method to unregister.</param>
    /// <typeparam name="T">Type of message to unregister.</typeparam>
    public void UnregisterMessageHandler<T>(Action<PlayerSession, T> handler) where T : INetMessage
    {
        Type key = typeof(T);
        
        if (_messageHandlers.TryGetValue(key, out MessageHandler? handlerCollection))
            handlerCollection.UnregisterAction(handler);
    }


    private void OnChatMessageReceived(PlayerSession session, ChatMessage msg)
    {
        Logger.LogInfo($"Received chat message from {session.Id}: {msg.Message}");
        
        // If the message is empty, ignore it.
        if (string.IsNullOrWhiteSpace(msg.Message))
            return;
        
        // Forward the message to all clients.
        SendMessageToAllClients(new ChatMessageNotification(session.PlayerData!.Username, msg.Message));
    }

#endregion


#region Sending messages

    public void SendMessageToClient<T>(PlayerSession client, T message, bool requireAuthenticated = true) where T : INetMessage
    {
        if (!IsStarted)
        {
            Logger.LogWarning("Cannot send message to client because server is not active.");
            return;
        }

        if (requireAuthenticated && !client.IsAuthenticated)
        {
            Logger.LogWarning($"Cannot send message {message} to client {client.Id} because they are not authenticated.");
            return;
        }

        client.QueueSend(message);
    }


    /// <summary>
    /// Sends a message to all clients.
    /// </summary>
    /// <param name="message">Packet data being sent.</param>
    /// <param name="requireAuthenticated">True if the client must be authenticated to receive this message.</param>
    /// <typeparam name="T">The type of message to send.</typeparam>
    public void SendMessageToAllClients<T>(T message, bool requireAuthenticated = true) where T : INetMessage
    {
        if (!IsStarted)
        {
            Logger.LogWarning("Cannot send message to clients because server is not active.");
            return;
        }

        foreach (PlayerSession c in _sessionManager.Sessions)
        {
            if (requireAuthenticated && !c.IsAuthenticated)
                continue;
            
            SendMessageToClient(c, message, requireAuthenticated);
        }
    }


    /// <summary>
    /// Sends a message to all clients except the specified one.
    /// </summary>
    public void SendMessageToAllClientsExcept<T>(T message, PlayerSession except, bool requireAuthenticated = true) where T : INetMessage
    {
        if (!IsStarted)
        {
            Logger.LogWarning("Cannot send message to clients because server is not active.");
            return;
        }

        foreach (PlayerSession c in _sessionManager.Sessions)
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
    public void SendMessageToAllClientsExcept<T>(T message, List<PlayerSession> except, bool requireAuthenticated = true) where T : INetMessage
    {
        if (!IsStarted)
        {
            Logger.LogWarning("Cannot send message to clients because server is not active.");
            return;
        }

        foreach (PlayerSession c in _sessionManager.Sessions)
        {
            if (except.Contains(c))
                continue;
            
            if (requireAuthenticated && !c.IsAuthenticated)
                continue;

            SendMessageToClient(c, message, requireAuthenticated);
        }
    }

#endregion


#region Packet processing

    public void ProcessPackets()
    {
        //TODO: Parallelize
        foreach (PlayerSession session in _sessionManager.Sessions)
        {
            session.IterateIncoming();
            session.IterateOutgoing();
        }
    }
    
    
    public void OnPacketReceived(PlayerSession session, Packet packet)
    {
        if (packet.Data.Array == null)
        {
            Logger.LogWarning($"Received a packet with null data. Kicking client {session.Id} immediately.");
            session.Kick(DisconnectReason.MalformedData);
            return;
        }
        
        if (packet.Data.Count > SharedConstants.MAX_PACKET_SIZE_BYTES)
        {
            Logger.LogWarning($"Received a packet that exceeds the maximum size. Kicking client {session.Id} immediately.");
            session.Kick(DisconnectReason.OversizedPacket);
            return;
        }
        
        INetMessage? msg = NetMessages.Deserialize(packet.Data);
        
        if (msg == null)
        {
            Logger.LogWarning($"Received a packet that could not be deserialized. Kicking client {session.Id} immediately.");
            session.Kick(DisconnectReason.MalformedData);
            return;
        }
        
        Type messageId = msg.GetType();
        
        Logger.LogDebug($"Received message {messageId} from client {session.Id}.");

        // Get handler.
        if (!_messageHandlers.TryGetValue(messageId, out MessageHandler? packetHandler))
        {
            Logger.LogWarning($"No handler is registered for {messageId}. Ignoring.");
            return;
        }

        if (packetHandler.RequiresAuthentication && !session.IsAuthenticated)
        {
            Logger.LogWarning($"Client {session.Id} sent a message of type {messageId} without being authenticated. Kicking.");
            session.Kick(DisconnectReason.ExploitAttempt);
            return;
        }
        
        // Invoke handler with message.
        packetHandler.Invoke(session, msg);
    }

#endregion


#region Server state

    private void OnServerStateChanged(ServerStateArgs args)
    {
        ServerState state = args.State;
        IsStarted = state == ServerState.Started;

        Logger.LogInfo($"Server is {state.ToString().ToLower()}");

        StateChanged?.Invoke(args);
    }

#endregion


#region Client state

    private void OnClientStateChanged(ClientStateArgs clientStateArgs)
    {
        ClientConnection connection = clientStateArgs.Connection;
        PlayerSession? session;
        
        Logger.LogDebug($"Client {connection.Id} is {clientStateArgs.State.ToString().ToLower()}");
        
        switch (clientStateArgs.State)
        {
            case ClientState.Connecting:
            {
                session = _sessionManager.StartSession(connection);
                
                Logger.LogInfo($"Player with session Id {session.Id} connecting!");

                if (_authenticator != null)
                    _authenticator.OnNewSession(session);
                else
                    OnClientAuthenticated(session);
                break;
            }
            case ClientState.Connected:
            {
                Debug.Assert(_sessionManager.HasSession(connection.Id), "Client connected but session was not created.");
                break;
            }
            case ClientState.Disconnecting:
            {
                _sessionManager.EndSession(connection.Id, out session);
                
                Logger.LogInfo($"Player with session Id {session!.Id} disconnecting!");

                // Only authenticated sessions have player data.
                if (session.IsAuthenticated)
                    SendMessageToAllClientsExcept(new ChatMessageNotification(session.PlayerData!.Username, "Left the chat."), session);
                break;
            }
            case ClientState.Disconnected:
            {
                Debug.Assert(!_sessionManager.HasSession(connection.Id), "Client disconnected but session was not removed.");
                break;
            }
        }
    }

#endregion


#region Authentication

    /// <summary>
    /// Called when the authenticator has concluded a result for a connection.
    /// </summary>
    /// <param name="session">The connection that was authenticated.</param>
    /// <param name="success">True if authentication passed, false if failed.</param>
    private void OnAuthenticatorResultConcluded(PlayerSession session, bool success)
    {
        if (success)
            OnClientAuthenticated(session);
        else
            session.Kick(DisconnectReason.AuthenticationFailed);
    }


    /// <summary>
    /// Called when a remote client authenticates with the server.
    /// </summary>
    private void OnClientAuthenticated(PlayerSession session)
    {
        Logger.LogInfo($"Client {session.Id} authenticated!");
        
        // Send the client a welcome message.
        session.QueueSend(new WelcomeMessage(session.Id.Value));
        
        // Load user data.
        if (!session.LoadPlayerData())
        {
            Logger.LogWarning($"Client {session.Id} player data could not be loaded.");
            session.Kick(DisconnectReason.CorruptPlayerData);
            return;
        }
        
        SendMessageToAllClientsExcept(new ChatMessageNotification(session.PlayerData!.Username, "Joined the chat."), session);
    }

#endregion
}