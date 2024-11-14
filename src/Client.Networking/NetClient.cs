using Client.Networking.HighLevel.Authentication;
using Client.Networking.LowLevel;
using Client.Networking.LowLevel.Transport;
using Shared;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Client.Networking;

public class NetClient
{
    private readonly INetClientTransport _transport;
    private readonly Authenticator _authenticator;
    private readonly MessageHandlerManager _messageHandlerManager;
    
    /// <summary>
    /// The current unique client ID.
    /// </summary>
    public ClientUid ClientUid { get; private set; }
    
    /// <summary>
    /// True if the local client is connected to the server.
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// True if the local client is authenticated with the server.
    /// </summary>
    public bool IsAuthenticated { get; private set; }
    
    /// <summary>
    /// Called after the local client connection state changes.
    /// </summary>
    public event Action<ConnectionStateArgs>? ConnectionStateChanged;

    /// <summary>
    /// Called after the local client has authenticated (when a <see cref="WelcomeMessage"/> is received from the server).
    /// </summary>
    public event Action? Authenticated;


    public NetClient(INetClientTransport transport)
    {
        _messageHandlerManager = new MessageHandlerManager();
        _transport = transport;
        _transport.ConnectionStateChanged += OnConnectionStateChanged;
        _transport.PacketReceived += OnPacketReceived;
        
        _authenticator = new Authenticator(this);
        
        RegisterMessageHandler<WelcomeMessage>(OnWelcomeReceived);
        RegisterMessageHandler<DisconnectMessage>(OnDisconnectReceived);
    }
    
    
    public void Connect()
    {
        Logger.LogInfo($"Connecting to {_transport.Address}:{_transport.Port}...");
        _transport.Connect();
    }


    public void Reconnect()
    {
        Logger.LogInfo("Reconnecting...");
        _transport.Reconnect();
    }


    /// <summary>
    /// Disconnects from the currently connected server.
    /// </summary>
    public void Disconnect()
    {
        Logger.LogInfo("Disconnecting...");
        _transport.Disconnect();
    }
    

    /// <summary>
    /// Registers a method to call when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">Method to call.</param>
    /// <typeparam name="T"></typeparam>
    public void RegisterMessageHandler<T>(Action<T> handler) where T : INetMessage => _messageHandlerManager.RegisterMessageHandler(handler);


    /// <summary>
    /// Unregisters a method from being called when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">The method to unregister.</param>
    /// <typeparam name="T">Type of message to unregister.</typeparam>
    public void UnregisterMessageHandler<T>(Action<T> handler) where T : INetMessage => _messageHandlerManager.UnregisterMessageHandler(handler);


    /// <summary>
    /// Sends a message to a connection.
    /// </summary>
    /// <typeparam name="T">Type of message to send.</typeparam>
    /// <param name="message">The message to send.</param>
    public void SendMessageToServer<T>(T message) where T : INetMessage
    {
        if (!IsConnected)
        {
            Logger.LogError($"Local connection is not started, cannot send message of type {message}.");
            return;
        }
        
        Logger.LogDebug($"Sending message {message} to server.");
        
        byte[] bytes = NetMessages.Serialize(message);
        
        if (bytes.Length > SharedConstants.MAX_PACKET_SIZE_BYTES)
        {
            Logger.LogError($"Message {message} exceeds maximum packet size of {SharedConstants.MAX_PACKET_SIZE_BYTES} bytes. Skipping.");
            return;
        }

        // Send the packet.
        _transport.SendAsync(bytes);
    }
    
    
    /// <summary>
    /// Called when the local client connection state changes.
    /// </summary>
    /// <param name="args">The new connection state.</param>
    private void OnConnectionStateChanged(ConnectionStateArgs args)
    {
        ConnectionState state = args.NewConnectionState;
        IsConnected = state == ConnectionState.Connected;
        IsAuthenticated = false;

        Logger.LogInfo($"Local client is {state.ToString().ToLower()}.");

        ConnectionStateChanged?.Invoke(args);
    }


    private void OnPacketReceived(Packet packet)
    {
        // Create a message instance.
        INetMessage? msg = NetMessages.Deserialize(packet.Data);
        
        if (msg == null)
        {
            Logger.LogWarning("Could not deserialize message from packet. Ignoring.");
            throw new NotImplementedException();
            return;
        }
        
        Logger.LogDebug($"Received message {msg} from server.");

        _messageHandlerManager.TryHandleMessage(msg);
    }


    private void OnWelcomeReceived(WelcomeMessage message)
    {
        Logger.LogInfo("Received welcome message from server.");
        
        ClientUid = new ClientUid(message.ClientId);

        // Mark local connection as authenticated.
        IsAuthenticated = true;
        Authenticated?.Invoke();
    }


    private void OnDisconnectReceived(DisconnectMessage message)
    {
        Logger.LogWarning($"Received disconnect message from server: {message.Reason}");
        
        // Disconnect the local client.
        Disconnect();
    }
}