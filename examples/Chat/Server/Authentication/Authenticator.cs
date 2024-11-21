using ScaleNet.Common;
using ScaleNet.Server;
using Server.Database;
using Shared.Authentication;
using NetMessages = Shared.NetMessages;
using SharedConstants = Shared.SharedConstants;

namespace Server.Authentication;

internal class Authenticator
{
    private readonly ServerNetworkManager<ClientConnection> _netManager;
    private readonly IDatabaseAccess _databaseAccess;
    private readonly bool _allowAccountRegistration;
    
    /// <summary>
    /// Called when authenticator has concluded that a client is authenticated.
    /// </summary>
    public event Action<ClientConnection>? ClientAuthSuccess;
    
    /// <summary>
    /// Called when authenticator has concluded that a client failed to authenticate.
    /// </summary>
    public event Action<ClientConnection>? ClientAuthFailure;
    
    /// <summary>
    /// Called when a client has successfully registered an account.
    /// </summary>
    public event Action<ClientConnection>? ClientRegistered;


    public Authenticator(ServerNetworkManager<ClientConnection> netManager, IDatabaseAccess databaseAccess, bool allowAccountRegistration)
    {
        _netManager = netManager;
        _databaseAccess = databaseAccess;
        _allowAccountRegistration = allowAccountRegistration;

        netManager.RegisterMessageHandler<NetMessages.AuthenticationRequestMessage>(OnReceiveAuthRequest);
        netManager.RegisterMessageHandler<NetMessages.RegisterRequestMessage>(OnReceiveRegisterRequest);
    }


    /// <summary>
    /// Called on the server immediately after a client connects. Can be used to send data to the client for authentication.
    /// </summary>
    /// <param name="connection">Connection which is not yet authenticated.</param>
    public void OnNewClientReadyForAuthentication(ClientConnection connection)
    {
        // Send the client the authentication info.
        _netManager.SendMessageToClient(connection, new NetMessages.AuthenticationInfoMessage(_allowAccountRegistration, SharedConstants.GAME_VERSION));
    }


    private void OnReceiveAuthRequest(ClientConnection connection, NetMessages.AuthenticationRequestMessage msg)
    {
        /* If a client is already authenticated, this could be an attack. Sessions
         * are removed when a client disconnects, so there is no reason they should
         * already be considered authenticated. */
        if (connection.IsAuthenticated)
        {
            connection.Kick(DisconnectReason.ExploitAttempt);
            return;
        }

        AuthenticationResult result = _databaseAccess.Authenticate(msg.Username, msg.Password, out AccountUID uid);
        
        if (result == AuthenticationResult.Success)
        {
            connection.SetAuthenticated(uid);
            
            // Invoke result. This is handled internally to complete the connection or kick client.
            ClientAuthSuccess?.Invoke(connection);
        }
        else
        {
            ClientAuthFailure?.Invoke(connection);
        }
        
        _netManager.SendMessageToClient(connection, new NetMessages.AuthenticationResponseMessage(result, uid.Value));
    }


    private void OnReceiveRegisterRequest(ClientConnection connection, NetMessages.RegisterRequestMessage msg)
    {
        // If a client is already authenticated, this could be an attack.
        if (connection.IsAuthenticated)
        {
            connection.Kick(DisconnectReason.ExploitAttempt);
            return;
        }
        
        if (!_allowAccountRegistration)
        {
            connection.Kick(DisconnectReason.ExploitAttempt);
            return;
        }

        AccountCreationResult result = _databaseAccess.CreateAccount(msg.Username, msg.Password);
        
        if (result == AccountCreationResult.Success)
            ClientRegistered?.Invoke(connection);
        
        _netManager.SendMessageToClient(connection, new NetMessages.RegisterResponseMessage(result));
    }
}