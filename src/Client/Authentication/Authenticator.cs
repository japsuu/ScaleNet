using Shared.Networking.Messages;
using Shared.Utils;

namespace Client.Authentication;

internal class Authenticator
{
    private readonly GameClient _client;
    private readonly string _password;


    public Authenticator(GameClient client, string password)
    {
        _client = client;
        _password = password;
        
        client.RegisterMessageHandler<AuthRequestMessage>(OnReceiveAuthRequestPacket);
    }


    private void OnReceiveAuthRequestPacket(AuthRequestMessage netMessage)
    {
        Logger.LogInfo("Received authentication request from server.");

        if (netMessage.AuthenticationMethod != 0)
        {
            Logger.LogError("Server requested an unsupported authentication method.");
            return;
        }
        
        string username = "development";
        string password = _password;
        
        // Ensure the username and password are within the 24-character limit.
        if (username.Length > 24)
            throw new InvalidOperationException("Username is too long.");
        if (password.Length > 24)
            throw new InvalidOperationException("Password is too long.");
        
        // Respond to the server with the password.
        AuthResponseMessage response = new(username, password);
        _client.SendMessageToServer(response);
    }
}