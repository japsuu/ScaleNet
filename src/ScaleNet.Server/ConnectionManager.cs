using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ScaleNet.Server.LowLevel.Transport;

namespace ScaleNet.Server;

public abstract class ConnectionManager<TConnection>(IServerTransport transport) where TConnection : Connection
{
    protected readonly ConcurrentDictionary<ConnectionId, TConnection> ClientsBySessionId = new();
    
    public IEnumerable<TConnection> Connections => ClientsBySessionId.Values;
    
    public int ConnectionCount => ClientsBySessionId.Count;
    
    
    protected abstract TConnection CreateConnection(ConnectionId connectionId, IServerTransport transport);
    
    
    public bool HasConnection(ConnectionId id)
    {
        return ClientsBySessionId.ContainsKey(id);
    }
    
    
    public bool TryGetConnection(ConnectionId id, [NotNullWhen(true)]out TConnection? connection)
    {
        return ClientsBySessionId.TryGetValue(id, out connection);
    }


    internal bool TryCreateConnection(ConnectionId connectionId, [NotNullWhen(true)]out TConnection? connection)
    {
        // Ensure that the connectionId is not already associated with a Connection
        if (HasConnection(connectionId))
        {
            connection = null;
            return false;
        }
        
        connection = CreateConnection(connectionId, transport);
        return ClientsBySessionId.TryAdd(connectionId, connection);
    }
    
    
    internal bool TryRemoveConnection(ConnectionId id, [NotNullWhen(true)]out TConnection? connection)
    {
        return ClientsBySessionId.TryRemove(id, out connection);
    }
}