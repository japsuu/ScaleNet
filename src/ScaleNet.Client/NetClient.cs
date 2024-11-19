using System;
using ScaleNet.Client.Authentication;
using ScaleNet.Client.LowLevel;
using ScaleNet.Client.LowLevel.Transport;
using ScaleNet.Utils;

namespace ScaleNet.Client
{
    public class NetClient
    {
        private readonly ILogger _logger;
        private readonly IClientTransport _transport;
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


        public NetClient(ILogger logger, IClientTransport transport)
        {
            NetMessages.Initialize(logger);
            
            _logger = logger;
            _messageHandlerManager = new MessageHandlerManager();
            _transport = transport;
            _transport.ConnectionStateChanged += OnConnectionStateChanged;
            _transport.MessageReceived += OnMessageReceived;
        
            _authenticator = new Authenticator(this);
            _authenticator.AuthenticationResultReceived += OnAuthenticationResultReceived;
            _authenticator.AccountCreationResultReceived += OnAccountCreationResultReceived;
        
            RegisterMessageHandler<AuthenticationInfoMessage>(OnReceiveAuthInfo);
            RegisterMessageHandler<DisconnectMessage>(OnDisconnectReceived);
        }


        public void Connect()
        {
            _logger.LogInfo($"Connecting to {_transport.Address}:{_transport.Port}...");
            _transport.Connect();
        }


        public void Reconnect()
        {
            _logger.LogInfo("Reconnecting...");
            _transport.Reconnect();
        }


        /// <summary>
        /// Disconnects from the currently connected server.
        /// </summary>
        public void Disconnect()
        {
            _logger.LogInfo("Disconnecting...");
            _transport.Disconnect();
        }
    
    
        internal void SetAuthenticated(AccountUID accountUid)
        {
            _logger.LogDebug("Local client is now authenticated.");
        
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
                _logger.LogError($"Local connection is not started, cannot send message of type {message}.");
                return;
            }
        
            _logger.LogDebug($"Sending message {message} to server.");

            // Send the message.
            _transport.SendAsync(message);
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

            _logger.LogInfo($"Local client is {state.ToString().ToLower()}.");

            ConnectionStateChanged?.Invoke(args);
        }


        private void OnMessageReceived(DeserializedNetMessage msg)
        {
            _logger.LogDebug($"Received message {msg} from server.");

            if (!_messageHandlerManager.TryHandleMessage(msg))
                _logger.LogWarning($"No handler is registered for {msg}. Ignoring.");
        }


        private void OnDisconnectReceived(DisconnectMessage message)
        {
            _logger.LogWarning($"Received disconnect message from server: {message.Reason}");
        
            // Disconnect the local client.
            Disconnect();
        }


        private void OnReceiveAuthInfo(AuthenticationInfoMessage msg)
        {
            if (msg.ServerVersion != SharedConstants.GAME_VERSION)
            {
                _logger.LogError($"The client version ({SharedConstants.GAME_VERSION}) does not match the server version ({msg.ServerVersion}). Please update the client.");
                Disconnect();
                return;
            }
        
            _serverAllowsRegistration = msg.RegistrationAllowed;

            TryAuthenticate();
        }


        private void OnAuthenticationResultReceived(AuthenticationResult result)
        {
            _logger.LogInfo($"Received authentication result: {result}");

            if (result == AuthenticationResult.Success)
                return;
        
            _logger.LogError("Authentication failed.");
            TryAuthenticate();
        }


        private void OnAccountCreationResultReceived(AccountCreationResult result)
        {
            _logger.LogInfo($"Received account creation result: {result}");
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
                    _logger.LogInfo("Do you want to login or register? (login/register)");
                    string? input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        _logger.LogError("Please enter 'login' or 'register'.");
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

                    _logger.LogError("Please enter 'login' or 'register'.");
                }
            }
            else
            {
                _logger.LogInfo("Registration is disabled by server. You can currently only login.");
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
                    _logger.LogError("Username and password cannot be empty.");
                    continue;
                }
            
                if (user.Length < SharedConstants.MIN_USERNAME_LENGTH || user.Length > SharedConstants.MAX_USERNAME_LENGTH)
                {
                    _logger.LogError($"Username must be between {SharedConstants.MIN_USERNAME_LENGTH} and {SharedConstants.MAX_USERNAME_LENGTH} characters.");
                    continue;
                }
        
                if (pass.Length < SharedConstants.MIN_PASSWORD_LENGTH || pass.Length > SharedConstants.MAX_PASSWORD_LENGTH)
                {
                    _logger.LogError($"Password must be between {SharedConstants.MIN_PASSWORD_LENGTH} and {SharedConstants.MAX_PASSWORD_LENGTH} characters.");
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
    
    
        private (string?, string?) RequestCredentials()
        {
            _logger.LogInfo("Enter your username:");
            string? username = Console.ReadLine();
        
            _logger.LogInfo("Enter your password:");
            string? password = Console.ReadLine();
        
            return (username, password);
        }
    }
}