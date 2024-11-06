using System.Collections.Concurrent;
using Server.Networking;
using Server.Networking.LowLevel;
using Shared;

namespace Server;

internal class SessionManager(GameServer server)
{
    private static uint nextSessionId = 0;

    private readonly ConcurrentDictionary<SessionId, PlayerSession> _sessionsBySessionId = new();
    private readonly ConcurrentDictionary<Guid, PlayerSession> _sessionsByConnectionId = new();
    
    public IEnumerable<PlayerSession> Sessions => _sessionsBySessionId.Values;


    public PlayerSession StartSession(ClientConnection connection)
    {
        uint uId = Interlocked.Increment(ref nextSessionId);
        SessionId id = new(uId);
        PlayerSession session = new(id, server, connection);
        
        _sessionsBySessionId.TryAdd(id, session);
        _sessionsByConnectionId.TryAdd(connection.Id, session);
        
        return session;
    }
    
    
    public bool TryGetSession(SessionId id, out PlayerSession? session)
    {
        return _sessionsBySessionId.TryGetValue(id, out session);
    }
    
    
    public bool TryGetSession(Guid connectionId, out PlayerSession? session)
    {
        return _sessionsByConnectionId.TryGetValue(connectionId, out session);
    }
    
    
    public void EndSession(PlayerSession session)
    {
        _sessionsBySessionId.TryRemove(session.Id, out _);
        _sessionsByConnectionId.TryRemove(session.ConnectionId, out _);
    }
    
    
    public void EndSession(SessionId id)
    {
        if (_sessionsBySessionId.TryRemove(id, out PlayerSession? session))
            _sessionsByConnectionId.TryRemove(session.ConnectionId, out _);
    }
    
    
    public void EndSession(Guid connectionId)
    {
        if (_sessionsByConnectionId.TryRemove(connectionId, out PlayerSession? session))
            _sessionsBySessionId.TryRemove(session.Id, out _);
    }
}