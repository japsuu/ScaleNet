using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Server.Networking.HighLevel;

internal class ClientManager(NetServer server)
{

    private readonly ConcurrentDictionary<SessionId, Client> _clientsByClientId = new();
    private readonly ConcurrentDictionary<Guid, Client> _clientsBySessionId = new();
    
    public IEnumerable<Client> Clients => _clientsByClientId.Values;


    public Client AddClient(ClientConnection connection)
    {
        // Ensure that the connection is not already associated with a session.
        if (_clientsBySessionId.ContainsKey(connection.Id))
            throw new InvalidOperationException("Connection is already associated with a session.");
        
        uint uId = Interlocked.Increment(ref nextClientId);
        //ClientId id = new(uId); // ID supplied by transport layer.
        Client session = new(id, server, connection);
        
        _clientsByClientId.TryAdd(id, session);
        _clientsBySessionId.TryAdd(connection.Id, session);
        
        return session;
    }
    
    
    public bool HasClient(SessionId id)
    {
        return _clientsByClientId.ContainsKey(id);
    }
    
    
    public bool HasClient(Guid connectionId)
    {
        return _clientsBySessionId.ContainsKey(connectionId);
    }
    
    
    public bool TryGetClient(SessionId id, [NotNullWhen(true)]out Client? session)
    {
        return _clientsByClientId.TryGetValue(id, out session);
    }
    
    
    public bool TryGetClient(Guid connectionId, [NotNullWhen(true)]out Client? session)
    {
        return _clientsBySessionId.TryGetValue(connectionId, out session);
    }
    
    
    public void RemoveClient(Client session)
    {
        _clientsByClientId.TryRemove(session.Id, out _);
        _clientsBySessionId.TryRemove(session.ConnectionId, out _);
    }
    
    
    public void RemoveClient(SessionId id, out Client? session)
    {
        if (_clientsByClientId.TryRemove(id, out session))
            _clientsBySessionId.TryRemove(session.ConnectionId, out _);
    }
    
    
    public void RemoveClient(Guid connectionId, out Client? session)
    {
        if (_clientsBySessionId.TryRemove(connectionId, out session))
            _clientsByClientId.TryRemove(session.Id, out _);
    }
}