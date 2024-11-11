using Client.Authentication;
using Client.Networking;
using Client.Networking.HighLevel;
using Client.Networking.LowLevel.Transport;
using Shared;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Client;

internal class GameClient
{
    private readonly CovTcpClient _covTcpClient;
    private readonly Authenticator _authenticator;
    private readonly MessageHandlerManager _messageHandlerManager;
    
    public SessionId SessionId { get; private set; }
    
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
    /// Called after local client has authenticated (when the client receives a welcome message from the server).
    /// </summary>
    public event Action? Authenticated;


    public GameClient(string address, int port)
    {
        _messageHandlerManager = new MessageHandlerManager();
        _covTcpClient = new CovTcpClient(address, port);
        _covTcpClient.ConnectionStateChanged += OnConnectionStateChanged;
        _covTcpClient.PacketReceived += OnPacketReceived;
        
        _authenticator = new Authenticator(this);
        
        RegisterMessageHandler<WelcomeMessage>(OnWelcomeReceived);
        RegisterMessageHandler<DisconnectMessage>(OnDisconnectReceived);
        RegisterMessageHandler<ChatMessageNotification>(msg => Logger.LogInfo($"[Chat] {msg.User}: {msg.Message}"));
    }
    
    
    public void Run()
    {
        Connect();

        Logger.LogInfo("'!' to exit");

        while (IsConnected)
        {
            _covTcpClient.ReceiveAsync();  // Iterate incoming
            
            if (!IsAuthenticated)
                continue;
            
            string? line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;
            
            if (line == "!")
                break;
            
            ClearPreviousConsoleLine();

            SendMessageToServer(new ChatMessage(line));
        }
    }


    public void Connect()
    {
        Logger.LogInfo($"Connecting to {_covTcpClient.Address}:{_covTcpClient.Port}...");
        _covTcpClient.Connect();
    }


    public void Reconnect()
    {
        Logger.LogInfo("Reconnecting...");
        _covTcpClient.Reconnect();
    }


    /// <summary>
    /// Disconnects from the currently connected server.
    /// </summary>
    public void Disconnect()
    {
        Logger.LogInfo("Disconnecting...");
        _covTcpClient.DisconnectAndStop();
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
        
        ReadOnlySpan<byte> span = new(bytes, 0, bytes.Length);

        // Send the packet.
        _covTcpClient.SendAsync(span);
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
        if (packet.Data.Array == null)
        {
            Logger.LogWarning("Received a packet with null data.");
            return;
        }
        
        // Create a message instance.
        INetMessage? msg = NetMessages.Deserialize(packet.Data);
        
        if (msg == null)
        {
            Logger.LogWarning("Could not deserialize message from packet. Ignoring.");
            return;
        }
        
        Logger.LogDebug($"Received message {msg} from server.");

        if (!_messageHandlerManager.TryHandleMessage(msg))
            Logger.LogWarning($"No handler is registered for {msg}. Ignoring.");
    }


    private void OnWelcomeReceived(WelcomeMessage message)
    {
        Logger.LogInfo("Received welcome message from server.");
        
        SessionId = new SessionId(message.SessionId);

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
    
    private static void ClearPreviousConsoleLine()
    {
        int currentLineCursor = Console.CursorTop - 1;
        Console.SetCursorPosition(0, Console.CursorTop - 1);
        for (int i = 0; i < Console.WindowWidth; i++)
            Console.Write(" ");
        Console.SetCursorPosition(0, currentLineCursor);
    }
}