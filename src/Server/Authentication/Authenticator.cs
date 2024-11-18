using Server.Authentication.Resolvers;
using Shared;
using Shared.Networking;

namespace Server.Authentication;

internal class Authenticator
{
    private readonly NetServer _server;
    private readonly IAuthenticationResolver _resolver;
    private readonly bool _allowAccountRegistration;
    
    /// <summary>
    /// Called when authenticator has concluded that a client is authenticated.
    /// </summary>
    public event Action<Client>? ClientAuthSuccess;
    
    /// <summary>
    /// Called when authenticator has concluded that a client failed to authenticate.
    /// </summary>
    public event Action<Client>? ClientAuthFailure;
    
    /// <summary>
    /// Called when a client has successfully registered an account.
    /// </summary>
    public event Action<Client>? ClientRegistered;


    public Authenticator(NetServer server, IAuthenticationResolver resolver, bool allowAccountRegistration)
    {
        _server = server;
        _resolver = resolver;
        _allowAccountRegistration = allowAccountRegistration;

        server.RegisterMessageHandler<AuthenticationRequestMessage>(OnReceiveAuthRequest, false);
        server.RegisterMessageHandler<RegisterRequestMessage>(OnReceiveRegisterRequest, false);
    }


    /// <summary>
    /// Called on the server immediately after a client connects. Can be used to send data to the client for authentication.
    /// </summary>
    /// <param name="client">Connection which is not yet authenticated.</param>
    public void OnNewClientConnected(Client client)
    {
        // Send the client the authentication info.
        _server.SendMessageToClient(client, new AuthenticationInfoMessage(_allowAccountRegistration, SharedConstants.GAME_VERSION), false);
    }


    private void OnReceiveAuthRequest(Client client, AuthenticationRequestMessage msg)
    {
        /* If a client is already authenticated, this could be an attack. Sessions
         * are removed when a client disconnects, so there is no reason they should
         * already be considered authenticated. */
        if (client.IsAuthenticated)
        {
            client.Kick(DisconnectReason.ExploitAttempt);
            return;
        }

        AuthenticationResult result = _resolver.TryAuthenticate(msg.Username, msg.Password, out AccountUID uid);
        
        if (result == AuthenticationResult.Success)
        {
            client.SetAuthenticated(uid);
            
            // Invoke result. This is handled internally to complete the connection or kick client.
            ClientAuthSuccess?.Invoke(client);
        }
        else
        {
            ClientAuthFailure?.Invoke(client);
        }
        
        _server.SendMessageToClient(client, new AuthenticationResponseMessage(result, uid.Value), false);
    }


    private void OnReceiveRegisterRequest(Client client, RegisterRequestMessage msg)
    {
        if (!_allowAccountRegistration)
        {
            client.Kick(DisconnectReason.ExploitAttempt);
            return;
        }
        
        // If a client is already authenticated, this could be an attack.
        if (client.IsAuthenticated)
        {
            client.Kick(DisconnectReason.ExploitAttempt);
            return;
        }

        AccountCreationResult result = _resolver.TryCreateAccount(msg.Username, msg.Password);
        
        if (result == AccountCreationResult.Success)
            ClientRegistered?.Invoke(client);
        
        _server.SendMessageToClient(client, new RegisterResponseMessage(result), false);
    }
}