using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using ScaleNet.Server.LowLevel.Transport;

namespace ScaleNet.Server;

public abstract class ConnectionManager<TConnection> where TConnection : Connection
{
    private readonly IServerTransport _transport;
    private readonly int _pingInterval;
    
    private long _lastPingTime = 0;
    
    protected readonly ConcurrentDictionary<ConnectionId, TConnection> ClientsBySessionId = new();
    
    public IEnumerable<TConnection> Connections => ClientsBySessionId.Values;
    public int ConnectionCount => ClientsBySessionId.Count;


    /// <summary>
    /// Creates a new connection manager.
    /// </summary>
    /// <param name="transport">The transport to create connections with.</param>
    /// <param name="pingInterval">The interval in milliseconds to send ping messages to clients.</param>
    protected ConnectionManager(IServerTransport transport, int pingInterval = 5000)
    {
        _transport = transport;
        _pingInterval = pingInterval;
    }
    
    
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
        
        connection = CreateConnection(connectionId, _transport);
        return ClientsBySessionId.TryAdd(connectionId, connection);
    }
    
    
    internal bool TryRemoveConnection(ConnectionId id, [NotNullWhen(true)]out TConnection? connection)
    {
        return ClientsBySessionId.TryRemove(id, out connection);
    }
    
    
    internal void PingConnections()
    {
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (currentTime - _lastPingTime < _pingInterval)
            return;
        
        foreach (TConnection connection in Connections)
        {
            if (connection.IsConnected == false)
                continue;
            
            // Send ping messages only to clients that have responded to the last ping.
            if (connection.IsWaitingForPong)
                connection.UpdateRTT(currentTime);
            else
                connection.SendPing();
        }

        _lastPingTime = currentTime;
    }
}