using Server.Networking.Database;
using Shared.Networking;

namespace Server.Networking.Authentication.Resolvers;

public class DatabaseAuthenticationResolver(IDatabaseAccess databaseAccess) : IAuthenticationResolver
{
    public AuthenticationResult TryAuthenticate(string user, string pass, out AccountUID accountUid) => databaseAccess.TryAuthenticate(user, pass, out accountUid);

    public AccountCreationResult TryCreateAccount(string username, string password) => databaseAccess.CreateAccount(username, password);
}