using Shared;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Client.Networking.HighLevel.Authentication;

internal class Authenticator
{
    private readonly GameClient _client;


    public Authenticator(GameClient client)
    {
        _client = client;
        
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
        
        Logger.LogInfo("Enter your username:");
        string? username = Console.ReadLine();
        
        if (string.IsNullOrEmpty(username))
            username = $"User-{RandomUtils.RandomString(6)}";
        
        Logger.LogInfo("Enter your password:");
        string? password = Console.ReadLine();
        
        if (string.IsNullOrEmpty(password))
            password = SharedConstants.DEVELOPMENT_AUTH_PASSWORD;
        
        // Ensure the username and password are within the 24-character limit.
        if (username.Length > 24)
            throw new InvalidOperationException("Username is too long.");
        if (password.Length > 24)
            throw new InvalidOperationException("Password is too long.");
        
        // Respond to the server with the password.
        AuthResponseMessage response = new(username, password, SharedConstants.GAME_VERSION);
        _client.SendMessageToServer(response);
    }
}