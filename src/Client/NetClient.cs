using Client.Authentication;
using Client.LowLevel;
using Client.LowLevel.Transport;
using Shared;
using Shared.Networking;
using Shared.Utils;

namespace Client;

public class NetClient
{
    private readonly INetClientTransport _transport;
    private readonly Authenticator _authenticator;
    private readonly MessageHandlerManager _messageHandlerManager;
    private bool _serverAllowsRegistration;

    /// <summary>
    /// The current unique client ID.
    /// </summary>
    public AccountUID AccountUid { get; private set; }
    
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
    /// Called after the local client has authenticated (when a <see cref="AuthenticationResponseMessage"/> with <see cref="AuthenticationResult.Success"/> is received from the server).
    /// </summary>
    public event Action? Authenticated;


    public NetClient(INetClientTransport transport)
    {
        _messageHandlerManager = new MessageHandlerManager();
        _transport = transport;
        _transport.ConnectionStateChanged += OnConnectionStateChanged;
        _transport.PacketReceived += OnPacketReceived;
        
        _authenticator = new Authenticator(this);
        _authenticator.AuthenticationResultReceived += OnAuthenticationResultReceived;
        _authenticator.AccountCreationResultReceived += OnAccountCreationResultReceived;
        
        RegisterMessageHandler<AuthenticationInfoMessage>(OnReceiveAuthInfo);
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
    
    
    internal void SetAuthenticated(AccountUID accountUid)
    {
        Logger.LogDebug("Local client is now authenticated.");
        
        IsAuthenticated = true;
        AccountUid = accountUid;
        Authenticated?.Invoke();
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


    private void OnDisconnectReceived(DisconnectMessage message)
    {
        Logger.LogWarning($"Received disconnect message from server: {message.Reason}");
        
        // Disconnect the local client.
        Disconnect();
    }


    private void OnReceiveAuthInfo(AuthenticationInfoMessage msg)
    {
        if (msg.ServerVersion != SharedConstants.GAME_VERSION)
        {
            Logger.LogError($"The client version ({SharedConstants.GAME_VERSION}) does not match the server version ({msg.ServerVersion}). Please update the client.");
            Disconnect();
            return;
        }
        
        _serverAllowsRegistration = msg.RegistrationAllowed;

        TryAuthenticate();
    }


    private void OnAuthenticationResultReceived(AuthenticationResult result)
    {
        Logger.LogInfo($"Received authentication result: {result}");

        if (result == AuthenticationResult.Success)
            return;
        
        Logger.LogError("Authentication failed.");
        TryAuthenticate();
    }


    private void OnAccountCreationResultReceived(AccountCreationResult result)
    {
        Logger.LogInfo($"Received account creation result: {result}");
        TryAuthenticate();
    }


    private void TryAuthenticate()
    {
        // Ask user if they want to log in or register.
        bool login;
        if (_serverAllowsRegistration)
        {
            while (true)
            {
                Logger.LogInfo("Do you want to login or register? (login/register)");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    Logger.LogError("Please enter 'login' or 'register'.");
                    continue;
                }

                if (input.Equals("login", StringComparison.OrdinalIgnoreCase))
                {
                    login = true;
                    break;
                }

                if (input.Equals("register", StringComparison.OrdinalIgnoreCase))
                {
                    login = false;
                    break;
                }

                Logger.LogError("Please enter 'login' or 'register'.");
            }
        }
        else
        {
            Logger.LogInfo("Registration is disabled by server. You can currently only login.");
            login = true;
        }
        
        // Ask user for username and password.
        string username;
        string password;
        while (true)
        {
            (string? user, string? pass) = RequestCredentials();
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            {
                Logger.LogError("Username and password cannot be empty.");
                continue;
            }
            
            if (user.Length < SharedConstants.MIN_USERNAME_LENGTH || user.Length > SharedConstants.MAX_USERNAME_LENGTH)
            {
                Logger.LogError($"Username must be between {SharedConstants.MIN_USERNAME_LENGTH} and {SharedConstants.MAX_USERNAME_LENGTH} characters.");
                continue;
            }
        
            if (pass.Length < SharedConstants.MIN_PASSWORD_LENGTH || pass.Length > SharedConstants.MAX_PASSWORD_LENGTH)
            {
                Logger.LogError($"Password must be between {SharedConstants.MIN_PASSWORD_LENGTH} and {SharedConstants.MAX_PASSWORD_LENGTH} characters.");
                continue;
            }
            
            username = user;
            password = pass;

            break;
        }

        if (login)
        {
            _authenticator.Login(username, password);
        }
        else
        {
            _authenticator.Register(username, password);
        }
    }
    
    
    private static (string?, string?) RequestCredentials()
    {
        Logger.LogInfo("Enter your username:");
        string? username = Console.ReadLine();
        
        Logger.LogInfo("Enter your password:");
        string? password = Console.ReadLine();
        
        return (username, password);
    }
}