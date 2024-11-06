using System.Collections.Concurrent;
using System.Net;
using NetStack.Serialization;
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
    private readonly ConcurrentDictionary<byte, MessageHandler> _messageHandlers;
    
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
        MessageManager.RegisterAllMessages();

        _tcpServer = new TcpGameServer(address, port);
        _sessionManager = new SessionManager(this);
        _messageHandlers = new ConcurrentDictionary<byte, MessageHandler>();
        
        _authenticator = new Authenticator(this, SharedConstants.DEVELOPMENT_AUTH_PASSWORD);
        _authenticator.AuthenticationResultConcluded += OnAuthenticatorResultConcluded;
        
        _tcpServer.ServerStateChanged += OnServerStateChanged;
        _tcpServer.ClientStateChanged += OnClientStateChanged;
    }


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


    /// <summary>
    /// Registers a method to call when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">Method to call.</param>
    /// <typeparam name="T"></typeparam>
    /// <param name="requiresAuthentication">True if the client must be authenticated to send this message.</param>
    public void RegisterMessageHandler<T>(Action<PlayerSession, T> handler, bool requiresAuthentication = true) where T : NetMessage
    {
        byte key = MessageManager.NetMessages.GetId<T>();

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
    public void UnregisterMessageHandler<T>(Action<PlayerSession, T> handler) where T : NetMessage
    {
        byte key = MessageManager.NetMessages.GetId<T>();
        
        if (_messageHandlers.TryGetValue(key, out MessageHandler? handlerCollection))
            handlerCollection.UnregisterAction(handler);
    }
    
    
    public void SendMessageToClient<T>(PlayerSession client, T message, bool requireAuthenticated = true) where T : NetMessage
    {
        if (!IsStarted)
        {
            Logger.LogWarning("Cannot send message to client because server is not active.");
            return;
        }

        if (requireAuthenticated && !client.IsAuthenticated)
        {
            Logger.LogWarning($"Cannot send message to client {client.Id} because they are not authenticated.");
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
    public void SendMessageToAllClients<T>(T message, bool requireAuthenticated = true) where T : NetMessage
    {
        if (!IsStarted)
        {
            Logger.LogWarning("Cannot send message to clients because server is not active.");
            return;
        }

        foreach (PlayerSession c in _sessionManager.Sessions)
            SendMessageToClient(c, message, requireAuthenticated);
    }


    /// <summary>
    /// Sends a message to all clients except the specified one.
    /// </summary>
    public void SendMessageToAllClientsExcept<T>(T message, PlayerSession except, bool requireAuthenticated = true)
        where T : NetMessage
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
            SendMessageToClient(c, message, requireAuthenticated);
        }
    }


    /// <summary>
    /// Sends a message to all clients except the specified ones.
    /// </summary>
    public void SendMessageToAllClientsExcept<T>(T message, List<PlayerSession> except, bool requireAuthenticated = true) where T : NetMessage
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
            SendMessageToClient(c, message, requireAuthenticated);
        }
    }


    public void ProcessPackets()
    {
        //TODO: Parallelize
        foreach (PlayerSession session in _sessionManager.Sessions)
        {
            session.IterateIncoming();
            session.IterateOutgoing();
        }
    }
    
    
    public void HandlePacket(PlayerSession session, Packet packet)
    {
        Logger.LogDebug($"Received segment {packet.Data.AsStringHex()} from client {session.Id}.");
        if (packet.Data.Array == null)
        {
            Logger.LogWarning($"Received a packet with null data. Kicking client {session.Id} immediately.");
            session.Kick(DisconnectReason.MalformedData);
            return;
        }
        
        BitBuffer buffer = PacketBufferPool.GetBitBuffer();
        buffer.FromArray(packet.Data.Array, packet.Data.Count);
        
        ParsePacket(session, buffer);
        
        buffer.Clear();
    }
    
    
    private void ParsePacket(PlayerSession session, BitBuffer buffer)
    {
        // Create a message instance.
        byte messageId = buffer.ReadByte();
        NetMessage netMessage = MessageManager.NetMessages.CreateInstance(messageId);
        
        // Deserialize to instance.
        MessageDeserializeResult deserializeResult = netMessage.Deserialize(buffer);
        if (deserializeResult != MessageDeserializeResult.Success)
        {
            Logger.LogWarning($"Failed to deserialize message {netMessage}. Reason: {deserializeResult}");

            switch (deserializeResult)
            {
                case MessageDeserializeResult.MalformedData:
                    session.Kick(DisconnectReason.MalformedData);
                    break;
                case MessageDeserializeResult.OutdatedVersion:
                    session.Kick(DisconnectReason.OutdatedVersion);
                    break;
                default:
                    session.Kick(DisconnectReason.UnexpectedProblem);
                    break;
            }
            
            return;
        }
        Logger.LogDebug($"Received message {netMessage} from client {session.Id}.");

        // Get handler.
        if (!_messageHandlers.TryGetValue(messageId, out MessageHandler? packetHandler))
        {
            Logger.LogWarning($"No handler is registered for {netMessage}. Ignoring.");
            return;
        }

        if (packetHandler.RequiresAuthentication && !session.IsAuthenticated)
        {
            Logger.LogWarning($"Client {session.Id} sent a message of type {netMessage} without being authenticated. Kicking.");
            session.Kick(DisconnectReason.ExploitAttempt);
            return;
        }
        
        // Invoke handler with message.
        packetHandler.Invoke(session, netMessage);
    }
    
    
    private void OnServerStateChanged(ServerStateArgs args)
    {
        ServerState state = args.State;
        IsStarted = state == ServerState.Started;

        Logger.LogInfo($"Server is {state.ToString().ToLower()}");

        StateChanged?.Invoke(args);
    }


    private void OnClientStateChanged(ClientStateArgs clientStateArgs)
    {
        ClientConnection connection = clientStateArgs.Connection;
        
        switch (clientStateArgs.State)
        {
            case ClientState.Connecting:
                PlayerSession session = _sessionManager.StartSession(connection);
                
                if (_authenticator != null)
                    _authenticator.OnNewSession(session);
                else
                    OnClientAuthenticated(session);
                
                break;
            case ClientState.Disconnecting:
                _sessionManager.EndSession(connection.Id);
                break;
        }
    }


    /// <summary>
    /// Called when the authenticator has concluded a result for a connection.
    /// </summary>
    /// <param name="session">The connection that was authenticated.</param>
    /// <param name="success">True if authentication passed, false if failed.</param>
    private static void OnAuthenticatorResultConcluded(PlayerSession session, bool success)
    {
        if (success)
            OnClientAuthenticated(session);
        else
            session.Kick(DisconnectReason.AuthenticationFailed);
    }


    /// <summary>
    /// Called when a remote client authenticates with the server.
    /// </summary>
    private static void OnClientAuthenticated(PlayerSession session)
    {
        if (session.IsAuthenticated)
        {
            Logger.LogWarning($"Client {session.Id} is already authenticated.");
            return;
        }
        
        session.SetAuthenticated();
        
        // Send the client a welcome message.
        WelcomeMessage message = new(session.Id);
        session.QueueSend(message);
    }
}