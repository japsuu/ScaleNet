using System.Diagnostics;
using ScaleNet.Server;
using ScaleNet.Server.LowLevel.Transport;
using Shared;
using Shared.Authentication;

namespace Server;

public class ClientConnection : Connection
{
    private AccountUID _accountId;

    public bool IsAuthenticated { get; private set; }

    public PlayerData? PlayerData { get; internal set; }


    public ClientConnection(ConnectionId connectionId, IServerTransport transport) : base(connectionId, transport)
    {
    }


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


    public void SetAuthenticated(AccountUID accountUid)
    {
        Debug.Assert(!IsAuthenticated, "Cannot authenticate a client that is already authenticated.");

        IsAuthenticated = true;
        AccountId = accountUid;
    }


    public void Kick(DisconnectReason reason) => Kick(new NetMessages.DisconnectMessage(reason));
}