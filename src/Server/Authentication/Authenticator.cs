using Server.Networking;
using Shared.Networking;
using Shared.Networking.Messages;

namespace Server.Authentication;

internal class Authenticator
{
    private readonly GameServer _server;
    private readonly string _password;
    
    /// <summary>
    /// Called when authenticator has concluded a result for a connection. Boolean is true if authentication passed, false if failed.
    /// The Server listens to this event automatically.
    /// </summary>
    public event Action<PlayerSession, bool>? AuthenticationResultConcluded;


    public Authenticator(GameServer server, string password)
    {
        _server = server;
        _password = password;
        
        server.RegisterMessageHandler<AuthResponseMessage>(OnReceiveAuthResponsePacket, false);
    }


    /// <summary>
    /// Called on the server immediately after a client connects. Can be used to send data to the client for authentication.
    /// </summary>
    /// <param name="session">Connection which is not yet authenticated.</param>
    public void OnNewSession(PlayerSession session)
    {
        // Send the client a authentication request.
        _server.SendMessageToClient(session, new AuthRequestMessage(AuthenticationMethod.UsernamePassword), false);
    }


    private void OnReceiveAuthResponsePacket(PlayerSession conn, AuthResponseMessage netMessage)
    {
        /* If a client is already authenticated, this could be an attack. Connections
         * are removed when a client disconnects, so there is no reason they should
         * already be considered authenticated. */
        if (conn.IsAuthenticated)
        {
            conn.Kick(DisconnectReason.ExploitAttempt);
            return;
        }

        bool validName = !string.IsNullOrWhiteSpace(netMessage.Username);
        bool isCorrectPassword = netMessage.Password == _password;
        bool isAuthSuccess = validName && isCorrectPassword;
        
        // Invoke result. This is handled internally to complete the connection or kick client.
        AuthenticationResultConcluded?.Invoke(conn, isAuthSuccess);
    }
}