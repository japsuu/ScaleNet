using Client.Networking;
using NetStack.Buffers;
using NetStack.Serialization;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Client;

internal class GameClient
{
    private readonly TcpGameClient _tcpClient;
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Create(1024, 64);
    private readonly Dictionary<byte, MessageHandler> _messageHandlers = new();


    public GameClient(string address, int port)
    {
        _tcpClient = new TcpGameClient(address, port);
        _tcpClient.ConnectionStateChanged += OnConnectionStateChanged;
        _tcpClient.PacketReceived += OnPacketReceived;
    }


    /// <summary>
    /// True if the local client is connected to the server.
    /// </summary>
    public bool IsStarted { get; private set; }

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
    
    
    public void Connect()
    {
        Console.WriteLine($"Client connecting to {_tcpClient.Address}:{_tcpClient.Port}");
        
        if (_tcpClient.ConnectAsync())
            Console.WriteLine("Done!");
        else
        {
            Console.WriteLine("Connection failed!");
        }
    }


    public void Reconnect()
    {
        Console.WriteLine("Client reconnecting...");
        _tcpClient.ReconnectAsync();
        Console.WriteLine("Done!");
    }


    /// <summary>
    /// Disconnects from the currently connected server.
    /// </summary>
    public void Disconnect()
    {
        Console.WriteLine("Client disconnecting...");
        _tcpClient.DisconnectAndStop();
        Console.WriteLine("Done!");
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
        if (!IsStarted)
        {
            Logger.LogError($"Local connection is not started, cannot send message of type {message}.");
            return;
        }

        // Write to buffer.
        BitBuffer buffer = PacketBufferPool.GetBitBuffer();
        buffer.AddByte((byte)InternalPacketType.Message);
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
        IsStarted = state == ConnectionState.Connected;

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
        InternalPacketType packetType = (InternalPacketType)buffer.ReadByte();
        
        switch (packetType)
        {
            case InternalPacketType.Unset:
                Logger.LogWarning("Received a packet with an unset type.");
                break;
            case InternalPacketType.Welcome:
                ParseWelcomePacket(buffer);
                break;
            case InternalPacketType.Message:
                ParseMessagePacket(buffer);
                break;
            case InternalPacketType.DisconnectNotification:
                Disconnect();
                break;
            default:
                Logger.LogWarning($"Received a message with an unknown packet type {packetType}.");
                break;
        }
        
        buffer.Clear();
    }
    
    
    private void ParseMessagePacket(BitBuffer buffer)
    {
        byte messageId = buffer.ReadByte();
        NetMessage netMessage = MessageManager.NetMessages.CreateInstance(messageId);
        netMessage.Deserialize(buffer);

        if (!_messageHandlers.TryGetValue(messageId, out MessageHandler? packetHandler))
        {
            Logger.LogWarning($"Received a {netMessage} but no handler is registered for it. Ignoring.");
            return;
        }
        
        Logger.LogDebug($"Received message {netMessage} from server.");

        packetHandler.InvokeHandlers(netMessage);
    }


    private void ParseWelcomePacket(BitBuffer buffer)
    {
        Logger.LogInfo("Received welcome message from server.");

        // Mark local connection as authenticated.
        IsAuthenticated = true;
        Authenticated?.Invoke();
    }
}