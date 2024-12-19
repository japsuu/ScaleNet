namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

public struct Message
{
    public readonly SessionId ConnId;
    public readonly EventType Type;
    public readonly ArrayBuffer? Data;
    public readonly Exception? Exception;


    public Message(SessionId connId, EventType type) : this()
    {
        ConnId = connId;
        Type = type;
    }


    public Message(SessionId connId, ArrayBuffer data) : this()
    {
        ConnId = connId;
        Type = EventType.Data;
        Data = data;
    }


    public Message(SessionId connId, Exception exception) : this()
    {
        ConnId = connId;
        Type = EventType.Error;
        Exception = exception;
    }
}