using Shared.Networking;

namespace Server.Networking.Authentication;

public interface IAuthenticationResolver
{
    public bool TryAuthenticate(string username, string password, out ClientUid clientUid);
}