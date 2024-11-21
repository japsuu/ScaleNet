using System;
using ScaleNet.Client.LowLevel;
using ScaleNet.Client.LowLevel.Transport;
using ScaleNet.Common;

namespace ScaleNet.Client
{
    public sealed class ClientNetworkManager : IDisposable
    {
        private readonly IClientTransport _transport;
        private readonly MessageHandlerManager _messageHandlerManager;

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


        public ClientNetworkManager(IClientTransport transport)
        {
            if(!Networking.IsInitialized)
                throw new InvalidOperationException("Networking.Initialize() must be called before creating a server.");

            _messageHandlerManager = new MessageHandlerManager();
            _transport = transport;
            _transport.ConnectionStateChanged += OnConnectionStateChanged;
            _transport.MessageReceived += OnMessageReceived;
        
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
        
        
        public void Dispose()
        {
            _transport.Dispose();
        }
    }
}