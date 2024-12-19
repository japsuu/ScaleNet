using System.Diagnostics;
using ScaleNet.Common;
using ScaleNet.Common.LowLevel;
using ScaleNet.Server.LowLevel.Transport;

namespace ScaleNet.Server;

/// <summary>
/// Represents a connection to a client.
/// </summary>
public abstract class Connection
{
    private readonly IServerTransport _transport;

    /// <summary>
    /// ID of the session/connection.
    /// Changes when the client reconnects.
    /// </summary>
    public readonly SessionId SessionId;
    
    public ConnectionState ConnectionState => _transport.GetConnectionState(SessionId);
    public bool IsConnected => ConnectionState == ConnectionState.Connected;
    
    
    protected Connection(SessionId sessionId, IServerTransport transport)
    {
        _transport = transport;
        SessionId = sessionId;
    }


    /// <summary>
    /// Disconnect the client.
    /// </summary>
    /// <param name="reason">The reason for the disconnection.</param>
    public void Kick(InternalDisconnectReason reason)
    {
        Debug.Assert(_transport != null, nameof(_transport) + " != null");
        
        ScaleNetManager.Logger.LogDebug($"Disconnecting client {SessionId} with reason {reason}.");

        _transport.StopConnection(SessionId, reason);
    }


    /// <summary>
    /// Immediately disconnects the client, without sending a message.
    /// </summary>
    public void KickImmediate()
    {
        Debug.Assert(_transport != null, nameof(_transport) + " != null");
        
        ScaleNetManager.Logger.LogDebug($"Disconnecting client {SessionId} immediately.");

        _transport.StopConnectionImmediate(SessionId);
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
        _transport.StopConnection(SessionId, InternalDisconnectReason.User);
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