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
    
    private long _lastSentPingTimestamp;
    
    internal bool IsWaitingForPong { get; private set; }
    
    public long RTT { get; private set; }

    /// <summary>
    /// ID of the session/connection.
    /// Changes when the client reconnects.
    /// </summary>
    public readonly ConnectionId ConnectionId;
    
    public ConnectionState ConnectionState => _transport.GetConnectionState(ConnectionId);
    public bool IsConnected => ConnectionState == ConnectionState.Connected;
    
    
    protected Connection(ConnectionId connectionId, IServerTransport transport)
    {
        _transport = transport;
        ConnectionId = connectionId;
    }


    internal void UpdateRTT(long currentUnixTime)
    {
        if (!IsWaitingForPong)
            return;
        
        RTT = currentUnixTime - _lastSentPingTimestamp;
    }


#region Server -> Client pinging

    internal void SendPing()
    {
        Debug.Assert(IsWaitingForPong, "Cannot send a ping while waiting for a pong.");
        
        _lastSentPingTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        QueueSend(new InternalPingMessage());
        IsWaitingForPong = true;
    }
    
    
    internal void OnPongReceived()
    {
        if (!IsWaitingForPong)
        {
            ScaleNetManager.Logger.LogWarning("Received a pong message when not expecting one.");
            return;
        }
        
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        UpdateRTT(currentTime);
        IsWaitingForPong = false;
    }

#endregion


#region Client -> Server pinging
    
    internal void OnPingReceived()
    {
        QueueSend(new InternalPongMessage());
    }

#endregion


    /// <summary>
    /// Disconnect the client.
    /// </summary>
    /// <param name="reason">The reason for the disconnection.</param>
    public void Kick(InternalDisconnectReason reason)
    {
        Debug.Assert(_transport != null, nameof(_transport) + " != null");
        
        ScaleNetManager.Logger.LogDebug($"Disconnecting client {ConnectionId} with reason {reason}.");

        _transport.StopConnection(ConnectionId, reason);
    }


    /// <summary>
    /// Immediately disconnects the client, without sending a message.
    /// </summary>
    public void KickImmediate()
    {
        Debug.Assert(_transport != null, nameof(_transport) + " != null");
        
        ScaleNetManager.Logger.LogDebug($"Disconnecting client {ConnectionId} immediately.");

        _transport.StopConnectionImmediate(ConnectionId);
    }


    /// <summary>
    /// Disconnect the client with a message.
    /// </summary>
    /// <param name="message">The message to send to the client before disconnecting.</param>
    public void Kick<T>(T message) where T : INetMessage
    {
        Debug.Assert(_transport != null, nameof(_transport) + " != null");
        
        ScaleNetManager.Logger.LogDebug($"Disconnecting client {ConnectionId}.");

        QueueSend(message);
        _transport.StopConnection(ConnectionId, InternalDisconnectReason.User);
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

        _transport.QueueSendAsync(ConnectionId, message);
    }
}