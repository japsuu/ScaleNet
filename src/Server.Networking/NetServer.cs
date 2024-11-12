using System.Collections.Concurrent;
using System.Diagnostics;
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
    private readonly ConcurrentDictionary<Type, MessageHandler> _messageHandlers;
    
    public readonly IServerTransport Transport;
    
    /// <summary>
    /// True if the server is started and listening for incoming connections.
    /// </summary>
    public bool IsStarted { get; private set; }

    /// <summary>
    /// Called after the server state changes.
    /// </summary>
    public event Action<ServerStateArgs>? StateChanged;


    public NetServer(IServerTransport transport)
    {
        Transport = transport;
        _clientManager = new ClientManager(this);
        _messageHandlers = new ConcurrentDictionary<Type, MessageHandler>();
        
        _authenticator = new Authenticator(this, SharedConstants.DEVELOPMENT_AUTH_PASSWORD);
        _authenticator.AuthenticationResultConcluded += OnAuthenticatorResultConcluded;
        
        Transport.ServerStateChanged += OnServerStateChanged;
        Transport.SessionStateChanged += OnSessionStateChanged;
        Transport.HandlePacket += OnPacketReceived;
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
            Transport.RejectNewPackets = true;

            foreach (Client session in _clientManager.Clients)
            {
                session.Kick(DisconnectReason.ServerShutdown);
            }
        }
        
        Transport.Stop();
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


#region Packet processing

    public void Update()
    {
        //BUG: Iterate transport
        Transport.IterateIncoming();
        Transport.IterateOutgoing();
        
        //TODO: Parallelize
        foreach (Client session in _clientManager.Clients)
        {
            session.IterateIncoming();
            session.IterateOutgoing();
        }
    }
    
    
    private void OnPacketReceived(SessionId sessionId, Packet packet)
    {
        if (!_clientManager.TryGetClient(sessionId, out Client? client))
        {
            Logger.LogWarning($"Received a packet from an unknown client {sessionId}. Ignoring.");
            return;
        }
        
        if (packet.Data.Length > SharedConstants.MAX_PACKET_SIZE_BYTES)
        {
            Logger.LogWarning($"Received a packet that exceeds the maximum size. Kicking client {client.Id} immediately.");
            client.Kick(DisconnectReason.OversizedPacket);
            return;
        }
        
        INetMessage? msg = NetMessages.Deserialize(packet.Data);
        
        if (msg == null)
        {
            Logger.LogWarning($"Received a packet that could not be deserialized. Kicking client {client.Id} immediately.");
            client.Kick(DisconnectReason.MalformedData);
            return;
        }
        
        Type messageId = msg.GetType();
        
        Logger.LogDebug($"Received message {messageId} from client {client.Id}.");

        // Get handler.
        if (!_messageHandlers.TryGetValue(messageId, out MessageHandler? packetHandler))
        {
            Logger.LogWarning($"No handler is registered for {messageId}. Ignoring.");
            return;
        }

        if (packetHandler.RequiresAuthentication && !client.IsAuthenticated)
        {
            Logger.LogWarning($"Client {client.Id} sent a message of type {messageId} without being authenticated. Kicking.");
            client.Kick(DisconnectReason.ExploitAttempt);
            return;
        }
        
        // Invoke handler with message.
        packetHandler.Invoke(client, msg);
    }

#endregion


#region Server state

    private void OnServerStateChanged(ServerStateArgs args)
    {
        ServerState state = args.NewState;
        IsStarted = state == ServerState.Started;

        Logger.LogInfo($"Server is {state.ToString().ToLower()}");

        StateChanged?.Invoke(args);
    }

#endregion


#region Client state

    private void OnSessionStateChanged(SessionStateArgs sessionStateArgs)
    {
        ClientConnection connection = sessionStateArgs.SessionId;
        Client? session;
        
        Logger.LogDebug($"Client {connection.Id} is {sessionStateArgs.State.ToString().ToLower()}");
        
        switch (sessionStateArgs.State)
        {
            case SessionState.Connecting:
            {
                session = _clientManager.AddClient(connection);
                
                Logger.LogInfo($"Player with session Id {session.Id} connecting!");

                if (_authenticator != null)
                    _authenticator.OnNewSession(session);
                else
                    OnClientAuthenticated(session);
                break;
            }
            case SessionState.Connected:
            {
                Debug.Assert(_clientManager.HasClient(connection.Id), "Client connected but session was not created.");
                break;
            }
            case SessionState.Disconnecting:
            {
                _clientManager.RemoveClient(connection.Id, out session);
                
                Logger.LogInfo($"Player with session Id {session!.Id} disconnecting!");

                // Only authenticated sessions have player data.
                if (session.IsAuthenticated)
                    SendMessageToAllClientsExcept(new ChatMessageNotification(session.PlayerData!.Username, "Left the chat."), session);
                break;
            }
            case SessionState.Disconnected:
            {
                Debug.Assert(!_clientManager.HasClient(connection.Id), "Client disconnected but session was not removed.");
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
    private void OnAuthenticatorResultConcluded(Client session, bool success)
    {
        if (success)
            OnClientAuthenticated(session);
        else
            session.Kick(DisconnectReason.AuthenticationFailed);
    }


    /// <summary>
    /// Called when a remote client authenticates with the server.
    /// </summary>
    private void OnClientAuthenticated(Client session)
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