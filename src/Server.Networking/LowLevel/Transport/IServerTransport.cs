using Server.Networking.HighLevel;
using Shared.Networking;
using Shared.Networking.Messages;

namespace Server.Networking.LowLevel.Transport;

public interface IServerTransport
{
    public int Port { get; }
    public int MaxConnections { get; }
    public bool RejectNewConnections { get; set; }
    public bool RejectNewMessages { get; set; }

    public event Action<ServerStateArgs>? ServerStateChanged;

    /// <summary>
    /// Called when the connection state of a client changes.
    /// </summary>
    public event Action<SessionStateArgs>? SessionStateChanged;

    /// <summary>
    /// Called to handle incoming messages.<br/>
    /// Implementations are required to be thread-safe, as this event may be raised from multiple threads.
    /// </summary>
    public event Action<SessionId, INetMessage>? HandleMessage;


    /// <summary>
    /// Queues the given message to be sent.
    /// The message will not be sent immediately, but the next time outgoing packets are iterated.
    /// </summary>
    /// <param name="sessionId">The session to send the message to.</param>
    /// <param name="message">The message to send.</param>
    public void QueueSendAsync<T>(SessionId sessionId, T message) where T : INetMessage;


    /// <summary>
    /// Disconnect the session.
    /// </summary>
    /// <param name="sessionId">The session to disconnect.</param>
    /// <param name="reason">The reason for the disconnection.</param>
    /// <param name="iterateOutgoing">True to send outgoing packets before disconnecting.</param>
    /// 
    /// <remarks>
    /// The disconnect reason will only be sent to the session if <paramref name="iterateOutgoing"/> is true.
    /// </remarks>
    public void DisconnectSession(SessionId sessionId, DisconnectReason reason, bool iterateOutgoing = true);


    /// <summary>
    /// Starts the server.
    /// </summary>
    /// <returns>True if the server was started successfully, false otherwise.</returns>
    public bool Start();


    /// <summary>
    /// Stops the server.
    /// </summary>
    /// <returns>True if the server was stopped, false if it was already stopped.</returns>
    public bool Stop();


    /// <summary>
    /// Handles incoming packets, calling <see cref="HandleMessage"/> for each received message.
    /// </summary>
    public void HandleIncomingMessages();


    /// <summary>
    /// Handles outgoing packets, sending all queued packets to their respective clients.
    /// </summary>
    public void HandleOutgoingMessages();
}