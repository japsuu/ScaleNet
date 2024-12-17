using System;
using ScaleNet.Server;

namespace JamesFrowen.SimpleWeb;

public struct Message
{
    public readonly SessionId connId;
    public readonly EventType type;
    public readonly ArrayBuffer data;
    public readonly Exception exception;

    public Message(EventType type) : this()
    {
        this.type = type;
    }

    public Message(ArrayBuffer data) : this()
    {
        type = EventType.Data;
        this.data = data;
    }

    public Message(Exception exception) : this()
    {
        type = EventType.Error;
        this.exception = exception;
    }

    public Message(SessionId connId, EventType type) : this()
    {
        this.connId = connId;
        this.type = type;
    }

    public Message(SessionId connId, ArrayBuffer data) : this()
    {
        this.connId = connId;
        type = EventType.Data;
        this.data = data;
    }

    public Message(SessionId connId, Exception exception) : this()
    {
        this.connId = connId;
        type = EventType.Error;
        this.exception = exception;
    }
}