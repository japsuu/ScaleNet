using Shared.Networking;

namespace Server.Networking.Authentication.Resolvers;

/// <summary>
/// Interface for classes that can authenticate clients.
/// </summary>
public interface IAuthenticationResolver
{
    public bool TryAuthenticate(string username, string password, out ClientUid clientUid);
}