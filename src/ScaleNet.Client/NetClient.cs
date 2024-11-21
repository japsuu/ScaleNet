using System;
using ScaleNet.Client.Authentication;
using ScaleNet.Client.LowLevel;
using ScaleNet.Client.LowLevel.Transport;
using ScaleNet.Common;

namespace ScaleNet.Client
{
    public sealed class NetClient : IDisposable
    {
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
        /// Called after the local client has received authentication information from the server.
        /// </summary>
        public event Action? ReceivedAuthInfo;

        /// <summary>
        /// Called after the local client has received an authentication result from the server.
        /// </summary>
        public event Action<AuthenticationResult>? AuthenticationResultReceived;

        /// <summary>
        /// Called after the local client has received an account creation result from the server.
        /// </summary>
        public event Action<AccountCreationResult>? AccountCreationResultReceived;


        public NetClient(IClientTransport transport)
        {
            if(!Networking.IsInitialized)
                throw new InvalidOperationException("Networking.Initialize() must be called before creating a server.");

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
            Networking.Logger.LogInfo($"Connecting to {_transport.Address}:{_transport.Port}...");
            _transport.ConnectClient();
        }


        public void Reconnect()
        {
            Networking.Logger.LogInfo("Reconnecting...");
            _transport.ReconnectClient();
        }


        /// <summary>
        /// Disconnects from the currently connected server.
        /// </summary>
        public void Disconnect()
        {
            Networking.Logger.LogInfo("Disconnecting...");
            _transport.DisconnectClient();
        }
    
    
        internal void SetAuthenticated(AccountUID accountUid)
        {
            Networking.Logger.LogDebug("Local client is now authenticated.");
        
            IsAuthenticated = true;
            AccountUid = accountUid;
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
                Networking.Logger.LogError($"Local connection is not started, cannot send message of type {message}.");
                return;
            }
        
            Networking.Logger.LogDebug($"SND - {message}");

            // Send the message.
            _transport.SendAsync(message);
        }
        
        
        public void RequestLogin(string username, string password)
        {
            if (!IsConnected)
            {
                Networking.Logger.LogError("Local connection is not started, cannot request login.");
                return;
            }

            if (IsAuthenticated)
            {
                Networking.Logger.LogError("Local client is already authenticated.");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Networking.Logger.LogError("Username and password cannot be empty.");
                return;
            }
            
            if (username.Length < SharedConstants.MIN_USERNAME_LENGTH || username.Length > SharedConstants.MAX_USERNAME_LENGTH)
            {
                Networking.Logger.LogError($"Username must be between {SharedConstants.MIN_USERNAME_LENGTH} and {SharedConstants.MAX_USERNAME_LENGTH} characters.");
                return;
            }
        
            if (password.Length < SharedConstants.MIN_PASSWORD_LENGTH || password.Length > SharedConstants.MAX_PASSWORD_LENGTH)
            {
                Networking.Logger.LogError($"Password must be between {SharedConstants.MIN_PASSWORD_LENGTH} and {SharedConstants.MAX_PASSWORD_LENGTH} characters.");
                return;
            }
        
            _authenticator.Login(username, password);
        }
        
        
        public void RequestRegister(string username, string password)
        {
            if (!IsConnected)
            {
                Networking.Logger.LogError("Local connection is not started, cannot request registration.");
                return;
            }

            if (IsAuthenticated)
            {
                Networking.Logger.LogError("Local client is already authenticated.");
                return;
            }

            if (!_serverAllowsRegistration)
            {
                Networking.Logger.LogInfo("Registration is disabled by server. You can currently only login.");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Networking.Logger.LogError("Username and password cannot be empty.");
                return;
            }
            
            if (username.Length < SharedConstants.MIN_USERNAME_LENGTH || username.Length > SharedConstants.MAX_USERNAME_LENGTH)
            {
                Networking.Logger.LogError($"Username must be between {SharedConstants.MIN_USERNAME_LENGTH} and {SharedConstants.MAX_USERNAME_LENGTH} characters.");
                return;
            }
        
            if (password.Length < SharedConstants.MIN_PASSWORD_LENGTH || password.Length > SharedConstants.MAX_PASSWORD_LENGTH)
            {
                Networking.Logger.LogError($"Password must be between {SharedConstants.MIN_PASSWORD_LENGTH} and {SharedConstants.MAX_PASSWORD_LENGTH} characters.");
                return;
            }
        
            _authenticator.Register(username, password);
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

            Networking.Logger.LogInfo($"Local client is {state.ToString().ToLower()}.");

            ConnectionStateChanged?.Invoke(args);
        }


        private void OnMessageReceived(DeserializedNetMessage msg)
        {
            Networking.Logger.LogDebug($"RCV - {msg.Type}");

            if (!_messageHandlerManager.TryHandleMessage(msg))
                Networking.Logger.LogWarning($"No handler is registered for {msg}. Ignoring.");
        }


        private void OnDisconnectReceived(DisconnectMessage message)
        {
            Networking.Logger.LogWarning($"Received disconnect message from server: {message.Reason}");
        
            // Disconnect the local client.
            Disconnect();
        }


        private void OnReceiveAuthInfo(AuthenticationInfoMessage msg)
        {
            _serverAllowsRegistration = msg.RegistrationAllowed;
            
            if (msg.ServerVersion != SharedConstants.GAME_VERSION)
            {
                Networking.Logger.LogError($"The client version ({SharedConstants.GAME_VERSION}) does not match the server version ({msg.ServerVersion}). Please update the client. Disconnecting.");
                Disconnect();
                return;
            }

            if (ReceivedAuthInfo == null)
            {
                Networking.Logger.LogWarning($"No handler is registered for {nameof(ReceivedAuthInfo)} event. Ignoring.");
                return;
            }

            ReceivedAuthInfo.Invoke();
        }


        private void OnAuthenticationResultReceived(AuthenticationResult result)
        {
            if (AuthenticationResultReceived == null)
            {
                Networking.Logger.LogWarning($"No handler is registered for {nameof(AuthenticationResultReceived)} event. Ignoring.");
                return;
            }
            
            AuthenticationResultReceived.Invoke(result);
        }


        private void OnAccountCreationResultReceived(AccountCreationResult result)
        {
            if (AccountCreationResultReceived == null)
            {
                Networking.Logger.LogWarning($"No handler is registered for {nameof(AccountCreationResultReceived)} event. Ignoring.");
                return;
            }
            
            AccountCreationResultReceived.Invoke(result);
        }
        
        
        public void Dispose()
        {
            _transport.Dispose();
        }
    }
}