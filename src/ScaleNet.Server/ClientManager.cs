using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ScaleNet.Server;

internal class ClientManager(NetServer server)
{
    private readonly ConcurrentDictionary<SessionId, Client> _clientsBySessionId = new();
    
    public IEnumerable<Client> Clients => _clientsBySessionId.Values;


    public bool TryAddClient(SessionId sessionId, [NotNullWhen(true)]out Client? session)
    {
        // Ensure that the session is not already associated with a Client.
        if (HasClient(sessionId))
        {
            session = null;
            return false;
        }
        
        session = new Client(sessionId, server);
        return _clientsBySessionId.TryAdd(sessionId, session);
    }
    
    
    public bool HasClient(SessionId id)
    {
        return _clientsBySessionId.ContainsKey(id);
    }
    
    
    public bool TryGetClient(SessionId id, [NotNullWhen(true)]out Client? session)
    {
        return _clientsBySessionId.TryGetValue(id, out session);
    }
    
    
    public bool TryRemoveClient(SessionId id, [NotNullWhen(true)]out Client? session)
    {
        return _clientsBySessionId.TryRemove(id, out session);
    }
}