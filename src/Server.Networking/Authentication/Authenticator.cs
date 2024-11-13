using Server.Networking.HighLevel;
using Shared;
using Shared.Networking;
using Shared.Networking.Messages;

namespace Server.Networking.Authentication;

internal class Authenticator
{
    private readonly NetServer _server;
    private readonly IAuthenticationResolver _resolver;
    
    /// <summary>
    /// Called when authenticator has concluded that a client is authenticated.
    /// </summary>
    public event Action<Client>? ClientAuthSuccess;
    
    /// <summary>
    /// Called when authenticator has concluded that a client failed to authenticate.
    /// The client will be kicked after this event is invoked.
    /// </summary>
    public event Action<Client>? ClientAuthFailure;


    public Authenticator(NetServer server, IAuthenticationResolver resolver)
    {
        _server = server;
        _resolver = resolver;
        
        server.RegisterMessageHandler<AuthResponseMessage>(OnReceiveAuthResponsePacket, false);
    }


    /// <summary>
    /// Called on the server immediately after a client connects. Can be used to send data to the client for authentication.
    /// </summary>
    /// <param name="client">Connection which is not yet authenticated.</param>
    public void OnNewClientConnected(Client client)
    {
        // Send the client an authentication request.
        _server.SendMessageToClient(client, new AuthRequestMessage(AuthenticationMethod.UsernamePassword), false);
    }


    private void OnReceiveAuthResponsePacket(Client client, AuthResponseMessage netMessage)
    {
        /* If a client is already authenticated, this could be an attack. Sessions
         * are removed when a client disconnects, so there is no reason they should
         * already be considered authenticated. */
        if (client.IsAuthenticated)
        {
            client.Kick(DisconnectReason.ExploitAttempt);
            return;
        }

        if (netMessage.Version != SharedConstants.GAME_VERSION)
        {
            client.Kick(DisconnectReason.OutdatedVersion);
            return;
        }

        if (_resolver.TryAuthenticate(netMessage.Username, netMessage.Password, out ClientUid uid))
        {
            client.SetAuthenticated(uid);
            
            // Invoke result. This is handled internally to complete the connection or kick client.
            ClientAuthSuccess?.Invoke(client);
        }
        else
        {
            ClientAuthFailure?.Invoke(client);
            
            client.Kick(DisconnectReason.AuthenticationFailed);
        }
    }
}