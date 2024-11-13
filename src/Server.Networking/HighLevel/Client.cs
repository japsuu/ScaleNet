using System.Diagnostics;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Server.Networking.HighLevel;

public class PlayerData(string username)
{
    public readonly string Username = username;
}

public class AuthenticationData(ClientUid clientUid)
{
    /// <summary>
    /// Unique ID of the client.
    /// Never changes, assigned on account creation.
    /// </summary>
    public readonly ClientUid ClientId = clientUid;
}

public class Client(SessionId sessionId, NetServer server)
{
    /// <summary>
    /// ID of the session/connection.
    /// Changes when the client reconnects.
    /// </summary>
    public readonly SessionId SessionId = sessionId;
    
    public bool IsAuthenticated { get; private set; }
    public bool IsDisconnecting { get; private set; }

    public AuthenticationData? AuthData { get; private set; }
    public PlayerData? PlayerData { get; private set; }


    public void SetAuthenticated(ClientUid clientUid)
    {
        Debug.Assert(!IsDisconnecting, "Cannot authenticate a disconnecting client.");
        Debug.Assert(!IsAuthenticated, "Cannot authenticate a client that is already authenticated.");

        IsAuthenticated = true;
        AuthData = new AuthenticationData(clientUid);
    }


    public bool LoadPlayerData()
    {
        Debug.Assert(!IsDisconnecting, "Cannot load player data for a disconnecting client.");
        Debug.Assert(IsAuthenticated, "Cannot load player data for an unauthenticated client.");

        if (AuthData == null)
        {
            Logger.LogError("Cannot load player data for an unauthenticated client.");
            return false;
        }
        
        //TODO: Load user data from a real database based on the client ID.
        if (!InMemoryMockDatabase.TryGetUsername(AuthData.ClientId, out string? username))
        {
            Logger.LogError("Failed to load username for client.");
            return false;
        }

        PlayerData = new PlayerData(username);
        return true;
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
        Debug.Assert(!IsDisconnecting, "Cannot disconnect a client that is already disconnecting.");

        Logger.LogDebug($"Disconnecting client {SessionId} with reason {reason}.");
        
        server.Transport.DisconnectSession(SessionId, reason, iterateOutgoing);
        
        IsDisconnecting = true;
    }


    public void QueueSend<T>(T message) where T : INetMessage
    {
        Debug.Assert(!IsDisconnecting, "Cannot send messages to a disconnecting client.");
        
        Logger.LogDebug($"Queue message {message} to client.");
        
        server.Transport.QueueSendAsync(SessionId, message);
    }
}