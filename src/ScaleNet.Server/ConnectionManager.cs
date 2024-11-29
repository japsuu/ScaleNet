using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ScaleNet.Server.LowLevel.Transport;

namespace ScaleNet.Server;

public abstract class ConnectionManager<TConnection>(IServerTransport transport) where TConnection : Connection
{
    protected readonly ConcurrentDictionary<SessionId, TConnection> ClientsBySessionId = new();
    
    public IEnumerable<TConnection> Connections => ClientsBySessionId.Values;
    
    
    protected abstract TConnection CreateConnection(SessionId sessionId, IServerTransport transport);
    
    
    public bool HasConnection(SessionId id)
    {
        return ClientsBySessionId.ContainsKey(id);
    }
    
    
    public bool TryGetConnection(SessionId id, [NotNullWhen(true)]out TConnection? connection)
    {
        return ClientsBySessionId.TryGetValue(id, out connection);
    }


    internal bool TryCreateConnection(SessionId sessionId, [NotNullWhen(true)]out TConnection? connection)
    {
        // Ensure that the session is not already associated with a Connection
        if (HasConnection(sessionId))
        {
            connection = null;
            return false;
        }
        
        connection = CreateConnection(sessionId, transport);
        return ClientsBySessionId.TryAdd(sessionId, connection);
    }
    
    
    internal bool TryRemoveConnection(SessionId id, [NotNullWhen(true)]out TConnection? connection)
    {
        return ClientsBySessionId.TryRemove(id, out connection);
    }
}