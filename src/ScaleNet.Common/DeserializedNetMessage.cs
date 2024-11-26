using System;

namespace ScaleNet.Common
{
    public readonly struct DeserializedNetMessage
    {
        public readonly INetMessage Message;
        public readonly Type Type;


        public DeserializedNetMessage(INetMessage message, Type type)
        {
            Message = message;
            Type = type;
        }
    }
}