using System.Diagnostics;
using ScaleNet.Common;
using ScaleNet.Server.LowLevel.Transport;

namespace ScaleNet.Server;

/// <summary>
/// Represents a connection to a client.
/// </summary>
public abstract class Connection
{
    private IServerTransport? _transport;
    
    /// <summary>
    /// ID of the session/connection.
    /// Changes when the client reconnects.
    /// </summary>
    public SessionId SessionId { get; private set; }
    
    
    internal void Initialize(SessionId sessionId, IServerTransport transport)
    {
        _transport = transport;
        SessionId = sessionId;
    }


    /// <summary>
    /// Disconnect the client.
    /// </summary>
    /// <param name="reason">The reason for the disconnection.</param>
    /// <param name="iterateOutgoing">True to send outgoing packets before disconnecting.</param>
    ///
    /// <remarks>
    /// The disconnect reason will only be sent to the client if <paramref name="iterateOutgoing"/> is true.
    /// </remarks>
    public void Kick(InternalDisconnectReason reason, bool iterateOutgoing = true)
    {
        Debug.Assert(_transport != null, nameof(_transport) + " != null");
        
        ScaleNetManager.Logger.LogDebug($"Disconnecting client {SessionId} with reason {reason}.");

        _transport.DisconnectSession(SessionId, reason, iterateOutgoing);
    }


    /// <summary>
    /// Disconnect the client with a message.
    /// </summary>
    /// <param name="message">The message to send to the client before disconnecting.</param>
    public void Kick<T>(T message) where T : INetMessage
    {
        Debug.Assert(_transport != null, nameof(_transport) + " != null");
        
        ScaleNetManager.Logger.LogDebug($"Disconnecting client {SessionId}.");

        QueueSend(message);
        _transport.DisconnectSession(SessionId, InternalDisconnectReason.User);
    }


    /// <summary>
    /// Queue a message to be sent to the client.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <typeparam name="T">The type of message to send.</typeparam>
    public void QueueSend<T>(T message) where T : INetMessage
    {
        Debug.Assert(_transport != null, nameof(_transport) + " != null");
        
        ScaleNetManager.Logger.LogDebug($"QUE - {message}");

        _transport.QueueSendAsync(SessionId, message);
    }
}