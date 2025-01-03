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
        private readonly int _pingInterval;
    
        private long _lastSentPingTimestamp;
        private bool _isWaitingForPong;
    
        public long RTT { get; private set; }
    
        /// <summary>
        /// True if the local client is connected to the server.
        /// </summary>
        public bool IsConnected { get; private set; }
    
        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        public event Action<ConnectionStateArgs>? ConnectionStateChanged;


        public ClientNetworkManager(IClientTransport transport, int pingInterval = 500)
        {
            if(!ScaleNetManager.IsInitialized)
                throw new InvalidOperationException("Networking.Initialize() must be called before creating a server.");

            _messageHandlerManager = new MessageHandlerManager();
            _transport = transport;
            _transport.ConnectionStateChanged += OnConnectionStateChanged;
            _transport.MessageReceived += OnMessageReceived;
            
            _pingInterval = pingInterval;
        
            RegisterMessageHandler<InternalDisconnectMessage>(OnDisconnectReceived);
            RegisterMessageHandler<InternalPingMessage>(_ => SendMessageToServer(new InternalPongMessage()));
            RegisterMessageHandler<InternalPongMessage>(OnPongReceived);
        }


        public void Connect()
        {
            ScaleNetManager.Logger.LogInfo($"Connecting to {_transport.Address}:{_transport.Port}...");
            _transport.ConnectClient();
        }


        public void Reconnect()
        {
            ScaleNetManager.Logger.LogInfo("Reconnecting...");
            _transport.ReconnectClient();
        }


        /// <summary>
        /// Disconnects from the currently connected server.
        /// </summary>
        public void Disconnect()
        {
            ScaleNetManager.Logger.LogInfo("Disconnecting...");
            _transport.DisconnectClient();
        }

    
        public void Update()
        {
            _transport.IterateIncoming();

            PingServer();
            
            _transport.IterateOutgoing();
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
                ScaleNetManager.Logger.LogError($"Local connection is not started, cannot send message of type {message}.");
                return;
            }
        
            ScaleNetManager.Logger.LogDebug($"SND - {message}");

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

            ScaleNetManager.Logger.LogInfo($"Local client is {state.ToString().ToLower()}.");

            ConnectionStateChanged?.Invoke(args);
        }


        private void OnMessageReceived(DeserializedNetMessage msg)
        {
            ScaleNetManager.Logger.LogDebug($"RCV - {msg.Type}");

            if (!_messageHandlerManager.TryHandleMessage(msg))
                ScaleNetManager.Logger.LogWarning($"No handler is registered for {msg.Type}. Ignoring.");
        }


        private void OnDisconnectReceived(InternalDisconnectMessage message)
        {
            ScaleNetManager.Logger.LogWarning($"Received disconnect message from server: {message.Reason}");
        
            // Disconnect the local client.
            Disconnect();
        }


        private void PingServer()
        {
            if (!_isWaitingForPong)
                return;
            
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (currentTime - _lastSentPingTimestamp < _pingInterval)
                return;

            SendMessageToServer(new InternalPingMessage());
            _lastSentPingTimestamp = currentTime;
            _isWaitingForPong = true;
        }


        private void OnPongReceived(InternalPongMessage msg)
        {
            if (!_isWaitingForPong)
            {
                ScaleNetManager.Logger.LogWarning("Received a pong message when not expecting one.");
                return;
            }
        
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            RTT = currentTime - _lastSentPingTimestamp;
            _isWaitingForPong = false;
        }
        
        
        public void Dispose()
        {
            _transport.Dispose();
        }
    }
}