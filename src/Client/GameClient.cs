﻿using Client.Authentication;
using Client.Networking;
using NetStack.Buffers;
using NetStack.Serialization;
using Shared;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Client;

internal class GameClient
{
    private readonly TcpGameClient _tcpClient;
    private readonly Authenticator _authenticator;
    private readonly ArrayPool<byte> _bufferPool;
    private readonly Dictionary<byte, MessageHandler> _messageHandlers;
    
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
        MessageManager.RegisterAllMessages();
        
        _bufferPool = ArrayPool<byte>.Create(1024, 64);
        _messageHandlers = new Dictionary<byte, MessageHandler>();
        _tcpClient = new TcpGameClient(address, port);
        _tcpClient.ConnectionStateChanged += OnConnectionStateChanged;
        _tcpClient.PacketReceived += OnPacketReceived;
        
        _authenticator = new Authenticator(this, SharedConstants.DEVELOPMENT_AUTH_PASSWORD);
        
        RegisterMessageHandler<WelcomeMessage>(OnWelcomeReceived);
    }
    
    
    public void Connect()
    {
        Logger.LogInfo($"Client connecting to {_tcpClient.Address}:{_tcpClient.Port}");
        
        if (_tcpClient.ConnectAsync())
            Logger.LogInfo("Done!");
        else
        {
            Logger.LogError("Connection failed!");
        }
    }


    public void Reconnect()
    {
        Logger.LogInfo("Client reconnecting...");
        _tcpClient.ReconnectAsync();
        Logger.LogInfo("Done!");
    }


    /// <summary>
    /// Disconnects from the currently connected server.
    /// </summary>
    public void Disconnect()
    {
        Logger.LogInfo("Client disconnecting...");
        _tcpClient.DisconnectAndStop();
        Logger.LogInfo("Done!");
    }
    

    /// <summary>
    /// Registers a method to call when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">Method to call.</param>
    /// <typeparam name="T"></typeparam>
    public void RegisterMessageHandler<T>(Action<T> handler) where T : NetMessage
    {
        byte key = MessageManager.NetMessages.GetId<T>();

        if (!_messageHandlers.TryGetValue(key, out MessageHandler? handlerCollection))
        {
            handlerCollection = new MessageHandler<T>();
            _messageHandlers.Add(key, handlerCollection);
        }

        handlerCollection.RegisterAction(handler);
    }


    /// <summary>
    /// Unregisters a method from being called when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">The method to unregister.</param>
    /// <typeparam name="T">Type of message to unregister.</typeparam>
    public void UnregisterMessageHandler<T>(Action<T> handler) where T : NetMessage
    {
        byte key = MessageManager.NetMessages.GetId<T>();
        
        if (_messageHandlers.TryGetValue(key, out MessageHandler? handlerCollection))
            handlerCollection.UnregisterAction(handler);
    }


    /// <summary>
    /// Sends a message to a connection.
    /// </summary>
    /// <typeparam name="T">Type of message to send.</typeparam>
    /// <param name="message">The message to send.</param>
    public void SendMessageToServer<T>(T message) where T : NetMessage
    {
        if (!IsConnected)
        {
            Logger.LogError($"Local connection is not started, cannot send message of type {message}.");
            return;
        }

        // Write to buffer.
        BitBuffer buffer = PacketBufferPool.GetBitBuffer();
        message.Serialize(buffer);
        
        Logger.LogDebug($"Sending message {message} to server.");
        
        int bufferLength = buffer.Length;
        
        // Get a pooled byte[] buffer.
        byte[] bytes = _bufferPool.Rent(bufferLength);
        
        // Get a ReadOnlySpan from the bytes.
        buffer.ToArray(bytes);
        ReadOnlySpan<byte> span = new(bytes, 0, bufferLength);

        // Send the packet.
        _tcpClient.SendAsync(span);
        
        // Return the buffer to the pool.
        _bufferPool.Return(bytes);
        
        buffer.Clear();
    }
    
    
    /// <summary>
    /// Called when the local client connection state changes.
    /// </summary>
    /// <param name="args">The new connection state.</param>
    private void OnConnectionStateChanged(ConnectionStateArgs args)
    {
        ConnectionState state = args.ConnectionState;
        IsConnected = state == ConnectionState.Connected;

        Logger.LogInfo($"Local client is {state.ToString().ToLower()}");

        ConnectionStateChanged?.Invoke(args);
    }


    private void OnPacketReceived(Packet packet)
    {
        Logger.LogDebug($"Received segment {packet.Data.AsStringHex()} from server.");
        if (packet.Data.Array == null)
        {
            Logger.LogWarning("Received a packet with null data.");
            return;
        }
        
        BitBuffer buffer = PacketBufferPool.GetBitBuffer();
        buffer.FromArray(packet.Data.Array, packet.Data.Count);
        
        ParsePacket(buffer);
        
        buffer.Clear();
    }
    
    
    private void ParsePacket(BitBuffer buffer)
    {
        // Create a message instance.
        byte messageId = buffer.ReadByte();
        NetMessage netMessage = MessageManager.NetMessages.CreateInstance(messageId);
        
        // Deserialize to instance.
        MessageDeserializeResult result = netMessage.Deserialize(buffer);
        if (result != MessageDeserializeResult.Success)
        {
            Logger.LogWarning($"Failed to deserialize message {netMessage}. Reason: {result}");
            return;
        }
        Logger.LogDebug($"Received message {netMessage} from server.");

        // Get handler.
        if (!_messageHandlers.TryGetValue(messageId, out MessageHandler? packetHandler))
        {
            Logger.LogWarning($"No handler is registered for {netMessage}. Ignoring.");
            return;
        }
        
        // Invoke handler with message.
        packetHandler.Invoke(netMessage);
    }


    private void OnWelcomeReceived(WelcomeMessage message)
    {
        Logger.LogInfo("Received welcome message from server.");

        // Mark local connection as authenticated.
        IsAuthenticated = true;
        Authenticated?.Invoke();
    }
}