using System.Diagnostics;
using ScaleNet.Networking;

namespace ScaleNet.Server;

public class Client(SessionId sessionId, NetServer server)
{
    private AccountUID _accountId;
    
    /// <summary>
    /// ID of the session/connection.
    /// Changes when the client reconnects.
    /// </summary>
    public readonly SessionId SessionId = sessionId;

    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// Unique ID of the account.
    /// Never changes, assigned on account creation.
    /// </summary>
    public AccountUID AccountId
    {
        get
        {
            Debug.Assert(IsAuthenticated, "Cannot get account ID for an unauthenticated client.");
            return _accountId;
        }
        private set => _accountId = value;
    }

    public PlayerData? PlayerData { get; internal set; }


    internal void SetAuthenticated(AccountUID accountUid)
    {
        Debug.Assert(!IsAuthenticated, "Cannot authenticate a client that is already authenticated.");

        IsAuthenticated = true;
        AccountId = accountUid;
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
    public void Kick(DisconnectReason reason, bool iterateOutgoing = true)
    {
        server.Logger.LogDebug($"Disconnecting client {SessionId} with reason {reason}.");
        
        server.Transport.DisconnectSession(SessionId, reason, iterateOutgoing);
    }


    public void QueueSend<T>(T message) where T : INetMessage
    {
        server.Logger.LogDebug($"Queue message {message} to client.");
        
        server.Transport.QueueSendAsync(SessionId, message);
    }
}