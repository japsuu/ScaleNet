using Shared.Networking;
using Shared.Networking.Messages;

namespace Client.Networking.HighLevel.Authentication;

internal class Authenticator
{
    private readonly NetClient _client;
    
    /// <summary>
    /// Called when authenticator has received an authentication result.
    /// </summary>
    public event Action<AuthenticationResult>? AuthenticationResultReceived;
    
    /// <summary>
    /// Called when authenticator has received a registration result.
    /// </summary>
    public event Action<AccountCreationResult>? AccountCreationResultReceived;


    public Authenticator(NetClient client)
    {
        _client = client;
        
        client.RegisterMessageHandler<AuthenticationResponseMessage>(OnReceiveAuthResponse);
        client.RegisterMessageHandler<RegisterResponseMessage>(OnReceiveRegisterResponse);
    }


    public void Login(string username, string password)
    {
        _client.SendMessageToServer(new AuthenticationRequestMessage(username, password));
    }


    public void Register(string username, string password)
    {
        _client.SendMessageToServer(new RegisterRequestMessage(username, password));
    }


    private void OnReceiveAuthResponse(AuthenticationResponseMessage msg)
    {
        if (msg.Result == AuthenticationResult.Success)
            _client.SetAuthenticated(new AccountUID(msg.ClientUid));

        AuthenticationResultReceived?.Invoke(msg.Result);
    }


    private void OnReceiveRegisterResponse(RegisterResponseMessage msg)
    {
        AccountCreationResultReceived?.Invoke(msg.Result);
    }
}