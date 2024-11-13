using Server.Networking.Database;
using Shared.Networking;

namespace Server.Networking.Authentication.Resolvers;

public class DefaultAuthenticationResolver(string password) : IAuthenticationResolver
{
    private uint _nextClientUid = 1;


    public bool TryAuthenticate(string user, string pass, out ClientUid clientUid)
    {
        if (pass == password)
        {
            //TODO: Load client ID from database.
            clientUid = new ClientUid(_nextClientUid);
            _nextClientUid++;
            
            InMemoryMockDatabase.AddUsername(clientUid, user);
            return true;
        }

        clientUid = ClientUid.Invalid;
        return false;
    }
}