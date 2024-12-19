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
    public event Action<SessionStateChangeArgs>? SessionStateChanged;

    /// <summary>
    /// Called to handle incoming messages.<br/>
    /// Implementations are required to be thread-safe, as this event may be raised from multiple threads.
    /// </summary>
    public event Action<SessionId, DeserializedNetMessage>? MessageReceived;


    /// <summary>
    /// Queues the given message to be sent.
    /// The message will not be sent immediately, but the next time outgoing packets are iterated.
    /// </summary>
    /// <param name="sessionId">The session to send the message to.</param>
    /// <param name="message">The message to send.</param>
    public void QueueSendAsync<T>(SessionId sessionId, T message) where T : INetMessage;


    /// <summary>
    /// Disconnects the session, sending a reason for the disconnection.
    /// </summary>
    /// <param name="sessionId">The session to disconnect.</param>
    /// <param name="reason">The reason for the disconnection.</param>
    /// 
    /// <returns>True if the session was disconnected, false if the session was not found.</returns>
    public bool StopConnection(SessionId sessionId, InternalDisconnectReason reason);


    /// <summary>
    /// Immediately disconnects the session, without sending any outgoing packets.
    /// </summary>
    /// 
    /// <returns>True if the session was disconnected, false if the session was not found.</returns>
    public bool StopConnectionImmediate(SessionId sessionId);


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
    /// Gets the connection state of the given session.
    /// </summary>
    /// <param name="sessionId">The session to get the connection state of.</param>
    /// <returns>The connection state of the session.</returns>
    public ConnectionState GetConnectionState(SessionId sessionId);


    /// <summary>
    /// Handles incoming packets, calling <see cref="MessageReceived"/> for each received message.
    /// </summary>
    public void IterateIncomingMessages();


    /// <summary>
    /// Handles outgoing packets, sending all queued packets to their respective clients.
    /// </summary>
    public void IterateOutgoingMessages();
}