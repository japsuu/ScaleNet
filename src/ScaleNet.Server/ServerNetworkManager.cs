﻿using ScaleNet.Common;
using ScaleNet.Server.LowLevel;
using ScaleNet.Server.LowLevel.Transport;

namespace ScaleNet.Server;

public sealed class ServerNetworkManager<TConnection> : IDisposable where TConnection : Connection, new()
{
    private readonly MessageHandlerManager<TConnection> _messageHandlerManager;
    private readonly ConnectionManager<TConnection> _connectionManager;

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
    public event Action<ClientStateChangeArgs<TConnection>>? ClientStateChanged;


    public ServerNetworkManager(IServerTransport transport)
    {
        if(!ScaleNetManager.IsInitialized)
            throw new InvalidOperationException("Networking.Initialize() must be called before creating a server.");

        Transport = transport;
        _messageHandlerManager = new MessageHandlerManager<TConnection>();
        _connectionManager = new ConnectionManager<TConnection>(Transport);
        
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

        foreach (TConnection c in _connectionManager.Connections)
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

        foreach (TConnection c in _connectionManager.Connections)
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

        foreach (TConnection c in _connectionManager.Connections)
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
        if (!_connectionManager.TryGetConnection(sessionId, out TConnection? connection))
        {
            ScaleNetManager.Logger.LogWarning($"Received a message from an unknown session {sessionId}. Ignoring, and ending the session.");
            Transport.DisconnectSession(sessionId, DisconnectReason.UnexpectedProblem);
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
                if (!_connectionManager.TryCreateConnection(sessionId, out connection))
                {
                    ScaleNetManager.Logger.LogWarning($"Client for session {sessionId} already exists. Kicking.");
                    Transport.DisconnectSession(sessionId, DisconnectReason.UnexpectedProblem);
                    return;
                }
                
                break;
            }
            case ConnectionState.Disconnected:
            {
                if (!_connectionManager.TryRemoveConnection(sessionId, out connection))
                {
                    ScaleNetManager.Logger.LogWarning($"Client for session {sessionId} not found in the client manager.");
                    return;
                }
                
                break;
            }
            case ConnectionState.SslHandshaking:
            case ConnectionState.Ready:
            case ConnectionState.Connected:
            case ConnectionState.Disconnecting:
            {
                if (!_connectionManager.TryGetConnection(sessionId, out connection))
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
        Transport.Dispose();
    }
}