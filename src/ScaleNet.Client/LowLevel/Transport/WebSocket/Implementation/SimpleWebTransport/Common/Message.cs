using System;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common
{
    public struct Message
    {
        public readonly EventType Type;
        public readonly ArrayBuffer? Data;
        public readonly Exception? Exception;


        public Message(EventType type) : this()
        {
            Type = type;
        }


        public Message(ArrayBuffer data) : this()
        {
            Type = EventType.Data;
            Data = data;
        }


        public Message(Exception exception) : this()
        {
            Type = EventType.Error;
            Exception = exception;
        }
    }
}