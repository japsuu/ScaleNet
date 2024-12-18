using ScaleNet.Common;
using ScaleNet.Common.LowLevel;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket;

public class WebSocketServerTransport : IServerTransport
{
    public int Port { get; }
    public int MaxConnections { get; }
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    public event Action<SessionStateChangeArgs>? SessionStateChanged;
    public event Action<SessionId, DeserializedNetMessage>? MessageReceived;


    public WebSocketServerTransport()
    {
        
    }
    
    
    public void Dispose()
    {
        
    }


#region Starting and stopping

    public bool StartServer()
    {
        
    }


    public bool StopServer(bool gracefully = true)
    {
        
    }

#endregion


#region Message iterating

    public void HandleIncomingMessages()
    {
        
    }


    public void HandleOutgoingMessages()
    {
        
    }

#endregion


#region Sending

    public void QueueSendAsync<T>(SessionId sessionId, T message) where T : INetMessage
    {
        
    }

#endregion


#region Utils

    public void DisconnectSession(SessionId sessionId, InternalDisconnectReason reason, bool iterateOutgoing = true)
    {
        
    }


    public ConnectionState GetConnectionState(SessionId sessionId)
    {
        
    }

#endregion
}