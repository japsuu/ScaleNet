using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ScaleNet.Server.LowLevel.Transport;

namespace ScaleNet.Server;

public class ConnectionManager<TConnection>(IServerTransport transport) where TConnection : Connection, new()
{
    private readonly ConcurrentDictionary<SessionId, TConnection> _clientsBySessionId = new();
    
    public IEnumerable<TConnection> Connections => _clientsBySessionId.Values;
    
    
    public bool HasConnection(SessionId id)
    {
        return _clientsBySessionId.ContainsKey(id);
    }
    
    
    public bool TryGetConnection(SessionId id, [NotNullWhen(true)]out TConnection? connection)
    {
        return _clientsBySessionId.TryGetValue(id, out connection);
    }


    internal bool TryCreateConnection(SessionId sessionId, [NotNullWhen(true)]out TConnection? connection)
    {
        // Ensure that the session is not already associated with a Connection
        if (HasConnection(sessionId))
        {
            connection = null;
            return false;
        }
        
        connection = new TConnection();
        connection.Initialize(sessionId, transport);
        return _clientsBySessionId.TryAdd(sessionId, connection);
    }
    
    
    internal bool TryRemoveConnection(SessionId id, [NotNullWhen(true)]out TConnection? connection)
    {
        return _clientsBySessionId.TryRemove(id, out connection);
    }
}