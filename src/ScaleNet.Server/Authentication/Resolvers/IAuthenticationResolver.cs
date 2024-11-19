namespace ScaleNet.Server.Authentication.Resolvers;

/// <summary>
/// Interface for classes that can authenticate clients.
/// </summary>
public interface IAuthenticationResolver
{
    public AuthenticationResult TryAuthenticate(string username, string password, out AccountUID accountUid);
    public AccountCreationResult TryCreateAccount(string username, string password);
}