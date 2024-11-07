using NetStack.Serialization;

namespace Shared.Networking.Messages;

public static class MessageManager
{
    public static void RegisterAllMessages()
    {
        // Use compiled lambda expressions to register messages.
        NetMessages.Register<AuthRequestMessage>(1, (buffer, message) =>
        {
            AuthRequestMessage msg = (AuthRequestMessage)message;
            buffer.AddByte((byte)msg.AuthenticationMethod);
            return true;
        }, buffer =>
        {
            AuthenticationMethod method = (AuthenticationMethod) buffer.ReadByte();
            return new AuthRequestMessage(method);
        });
        
        NetMessages.Register<AuthResponseMessage>(2, (buffer, message) =>
        {
            AuthResponseMessage msg = (AuthResponseMessage)message;
            buffer.AddString(msg.Username);
            buffer.AddString(msg.Password);
            return true;
        }, buffer =>
        {
            string username = buffer.ReadString();
            string password = buffer.ReadString();
            return new AuthResponseMessage(username, password);
        });
        
        NetMessages.Register<WelcomeMessage>(3, (buffer, message) =>
        {
            WelcomeMessage msg = (WelcomeMessage)message;
            buffer.AddUInt(msg.SessionId.Value);
            return true;
        }, buffer =>
        {
            SessionId sessionId = new(buffer.ReadUInt());
            return new WelcomeMessage(sessionId);
        });
        
        NetMessages.Register<SessionInitiateMessage>(2, (buffer, message) =>
        {
            SessionInitiateMessage msg = (SessionInitiateMessage)message;
            buffer.AddUShort(msg.Version);
            return true;
        }, buffer =>
        {
            ushort version = buffer.ReadUShort();
        
            if (version != SharedConstants.GAME_VERSION)
                return null;
            return new SessionInitiateMessage(version);
        });
        
        /*NetMessages.Register<AuthResultMessage>(4, (buffer, message) =>
        {
            AuthResultMessage msg = (AuthResultMessage)message;
            buffer.AddByte((byte)msg.Result);
            return true;
        }, buffer =>
        {
            AuthResult result = (AuthResult) buffer.ReadByte();
            return new AuthResultMessage(result);
        });*/
    }


    /// <summary>
    /// Provides methods for retrieving message IDs for message types.
    /// </summary>
    public static class NetMessages
    {
        private static readonly Dictionary<Type, byte> MessageIds = new();
        private static readonly Dictionary<byte, Func<BitBuffer, INetMessage, bool>> Serializers = new();
        private static readonly Dictionary<byte, Func<BitBuffer, INetMessage?>> Deserializers = new();

        public static byte GetId<T>() => GetId(typeof(T));


        public static byte GetId(Type type)
        {
            if (MessageIds.TryGetValue(type, out byte id))
                return id;
            
            throw new InvalidOperationException($"No message ID found for type {type}.");
        }


        public static bool Serialize<T>(T message, BitBuffer buffer) where T : INetMessage
        {
            byte id = GetId<T>();

            buffer.AddByte(id);
            Serializers[id](buffer, message);
            
            return true;
        }


        public static INetMessage? Deserialize(BitBuffer buffer, out byte id)
        {
            id = buffer.ReadByte();
            
            if (Deserializers.TryGetValue(id, out Func<BitBuffer, INetMessage?>? creator))
                return creator(buffer);

            return null;
        }
        
        
        internal static void Register<T>(byte id, Func<BitBuffer, INetMessage, bool> serializer, Func<BitBuffer, INetMessage?> deserializer)
        {
            Type type = typeof(T);
            
            MessageIds[type] = id;
            Serializers[id] = serializer;
            Deserializers[id] = deserializer;
        }
    }
}