using Server.Networking.HighLevel;
using Shared;
using Shared.Networking;
using Shared.Networking.Messages;

namespace Server.Networking.Authentication;

internal class Authenticator
{
    private readonly NetServer _server;
    private readonly string _password;
    
    /// <summary>
    /// Called when authenticator has concluded a result for a connection. Boolean is true if authentication passed, false if failed.
    /// The Server listens to this event automatically.
    /// </summary>
    public event Action<Client, bool>? AuthenticationResultConcluded;


    public Authenticator(NetServer server, string password)
    {
        _server = server;
        _password = password;
        
        server.RegisterMessageHandler<AuthResponseMessage>(OnReceiveAuthResponsePacket, false);
    }


    /// <summary>
    /// Called on the server immediately after a client connects. Can be used to send data to the client for authentication.
    /// </summary>
    /// <param name="session">Connection which is not yet authenticated.</param>
    public void OnNewSession(Client session)
    {
        // Send the client a authentication request.
        _server.SendMessageToClient(session, new AuthRequestMessage(AuthenticationMethod.UsernamePassword), false);
    }


    private void OnReceiveAuthResponsePacket(Client session, AuthResponseMessage netMessage)
    {
        /* If a client is already authenticated, this could be an attack. Sessions
         * are removed when a client disconnects, so there is no reason they should
         * already be considered authenticated. */
        if (session.IsAuthenticated)
        {
            session.Kick(DisconnectReason.ExploitAttempt);
            return;
        }

        if (netMessage.Version != SharedConstants.GAME_VERSION)
        {
            session.Kick(DisconnectReason.OutdatedVersion);
            return;
        }

        bool validName = !string.IsNullOrWhiteSpace(netMessage.Username);
        bool isCorrectPassword = netMessage.Password == _password;
        bool isAuthSuccess = validName && isCorrectPassword;

        if (isAuthSuccess)
        {
            // TODO: Fetch the player's personal ID from the database, based on the credentials.
            string pId = netMessage.Username;
            
            session.SetAuthenticated(pId);
        }
        
        // Invoke result. This is handled internally to complete the connection or kick client.
        AuthenticationResultConcluded?.Invoke(session, isAuthSuccess);
    }
}