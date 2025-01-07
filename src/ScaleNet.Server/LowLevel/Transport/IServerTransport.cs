using ScaleNet.Common;
using ScaleNet.Common.LowLevel;

namespace ScaleNet.Server.LowLevel.Transport;

/// <summary>
/// Represents a server transport that can send and receive network messages.
/// </summary>
public interface IServerTransport : IDisposable
{
    public ushort Port { get; }
    public int MaxConnections { get; }
    public ServerState State { get; }

    public event Action<ServerStateChangeArgs>? ServerStateChanged;

    /// <summary>
    /// Called when the connection state of a client changes.
    /// </summary>
    public event Action<ConnectionStateChangeArgs>? RemoteConnectionStateChanged;

    /// <summary>
    /// Called to handle incoming messages.<br/>
    /// Implementations are required to be thread-safe, as this event may be raised from multiple threads.
    /// </summary>
    public event Action<ConnectionId, DeserializedNetMessage>? MessageReceived;


    /// <summary>
    /// Queues the given message to be sent.
    /// The message will not be sent immediately, but the next time outgoing packets are iterated.
    /// </summary>
    /// <param name="connectionId">The connectionId to send the message to.</param>
    /// <param name="message">The message to send.</param>
    public void QueueSendAsync<T>(ConnectionId connectionId, T message) where T : INetMessage;


    /// <summary>
    /// Disconnects the connectionId, sending a reason for the disconnection.
    /// </summary>
    /// <param name="connectionId">The connectionId to disconnect.</param>
    /// <param name="reason">The reason for the disconnection.</param>
    /// 
    /// <returns>True if the connectionId was disconnected, false if the connectionId was not found.</returns>
    public bool StopConnection(ConnectionId connectionId, InternalDisconnectReason reason);


    /// <summary>
    /// Immediately disconnects the connectionId, without sending any outgoing packets.
    /// </summary>
    /// 
    /// <returns>True if the connectionId was disconnected, false if the connectionId was not found.</returns>
    public bool StopConnectionImmediate(ConnectionId connectionId);


    /// <summary>
    /// Starts the server.
    /// </summary>
    /// <returns>True if the server was started successfully, false otherwise.</returns>
    public bool StartServer();


    /// <summary>
    /// Stops the server.
    /// </summary>
    /// <returns>True if the server was stopped, false if it was already stopped.</returns>
    public bool StopServer();


    /// <summary>
    /// Gets the connection state of the given connectionId.
    /// </summary>
    /// <param name="connectionId">The connectionId to get the connection state of.</param>
    /// <returns>The connection state of the connectionId.</returns>
    public ConnectionState GetConnectionState(ConnectionId connectionId);


    /// <summary>
    /// Handles incoming packets, calling <see cref="MessageReceived"/> for each received message.
    /// </summary>
    public void IterateIncomingMessages();


    /// <summary>
    /// Handles outgoing packets, sending all queued packets to their respective clients.
    /// </summary>
    public void IterateOutgoingMessages();
}