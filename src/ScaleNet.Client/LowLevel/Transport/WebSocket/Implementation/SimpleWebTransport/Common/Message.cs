using System;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common
{
    public struct Message
    {
        public readonly ConnectionId ConnId;
        public readonly EventType Type;
        public readonly ArrayBuffer? Data;
        public readonly Exception? Exception;


        public Message(ConnectionId connId, EventType type) : this()
        {
            ConnId = connId;
            Type = type;
        }


        public Message(ConnectionId connId, ArrayBuffer data) : this()
        {
            ConnId = connId;
            Type = EventType.Data;
            Data = data;
        }


        public Message(ConnectionId connId, Exception exception) : this()
        {
            ConnId = connId;
            Type = EventType.Error;
            Exception = exception;
        }
    }
}