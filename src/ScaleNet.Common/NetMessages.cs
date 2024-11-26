using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using MessagePack;
using ScaleNet.Common.Utils;

namespace ScaleNet.Common
{
    /// <summary>
    /// Marks a class as a network message.
    /// </summary>
    public sealed class NetMessageAttribute : Attribute
    {
        public readonly ushort Id;


        /// <summary>
        /// Marks a class as a network message.
        /// </summary>
        /// <param name="id">The unique ID of the message. IDs 65000 and above are reserved for internal use.</param>
        public NetMessageAttribute(ushort id)
        {
            Id = id;
        }
    }
    
    public static class NetMessages
    {
        private static readonly Dictionary<ushort, Type> MessageTypes = new();
        private static readonly Dictionary<Type, ushort> MessageIds = new();
        

        internal static void Initialize()
        {
            RegisterAllMessages();
        }
        
        
        private static void RegisterAllMessages()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly assembly in assemblies)
                RegisterAssembly(assembly);
        }


        private static void RegisterAssembly(Assembly assembly)
        {
            Type[] types = assembly.GetTypes();

            foreach (Type type in types)
            {
                if (!type.GetInterfaces().Contains(typeof(INetMessage)))
                    continue;

                RegisterINetMessage(type);
            }
        }


        private static void RegisterINetMessage(Type type)
        {
            NetMessageAttribute? netMessageAttribute = type.GetCustomAttribute<NetMessageAttribute>();
            MessagePackObjectAttribute? messagePackObjectAttribute = type.GetCustomAttribute<MessagePackObjectAttribute>();

            if (netMessageAttribute == null)
            {
                ScaleNetManager.Logger.LogError($"Message {type} is missing the NetMessage attribute.");
                return;
            }
            
            if (messagePackObjectAttribute == null)
            {
                ScaleNetManager.Logger.LogError($"Message {type} is missing the MessagePackObject attribute.");
                return;
            }

            if (MessageTypes.TryGetValue(netMessageAttribute.Id, out Type? msgType))
            {
                ScaleNetManager.Logger.LogError($"Message ID {netMessageAttribute.Id} is already in use by {msgType}.");
                return;
            }

            if (type.IsAbstract)
            {
                ScaleNetManager.Logger.LogError($"Message {type} must not be abstract.");
                return;
            }

            if (type.IsClass)
            {
                ScaleNetManager.Logger.LogError($"Message {type} must be a struct.");
                return;
            }

            MessageTypes.Add(netMessageAttribute.Id, type);
            MessageIds.Add(type, netMessageAttribute.Id);
            
            ScaleNetManager.Logger.LogInfo($"Registered message {type} with ID {netMessageAttribute.Id}.");
        }


        public static bool TryGetMessageType(ushort id, out Type type)
        {
            return MessageTypes.TryGetValue(id, out type);
        }
        
        
        public static bool TryGetMessageId(Type type, out ushort id)
        {
            return MessageIds.TryGetValue(type, out id);
        }


#region Serialization
        
        public static byte[] Serialize<T>(T msg)
        {
            byte[] bytes = MessagePackSerializer.Serialize(msg);
        
            return bytes;
        }

        
        public static void Serialize<T>(T msg, IBufferWriter<byte> writer)
        {
            MessagePackSerializer.Serialize(writer, msg);
        }


        /*public static bool TryDeserialize(ushort id, byte[] buffer, out DeserializedNetMessage message)
        {
            return TryDeserialize(id, buffer.AsMemory(), out message);
        }*/


        public static bool TryDeserialize(ushort id, ReadOnlyMemory<byte> buffer, out DeserializedNetMessage message)
        {
            if (!TryGetMessageType(id, out Type type))
            {
                ScaleNetManager.Logger.LogError($"Failed to deserialize message with ID {id}: No message type found.");
                message = default;
                return false;
            }
            
            try
            {
                object? msg = MessagePackSerializer.Deserialize(type, buffer);
                Debug.Assert(msg != null, "msg != null");
                
                INetMessage? netMsg = msg as INetMessage;
                Debug.Assert(netMsg != null, "netMsg != null");

                message = new DeserializedNetMessage(netMsg, type);
                return true;
            }
            catch (Exception e)
            {
                ScaleNetManager.Logger.LogError($"Failed to deserialize message {type}: {buffer.AsStringBits()}:\n{e}");
                message = default;
                return false;
            }
        }

#endregion
    }
    
    
    /// <summary>
    /// Inherit to define a network message that can be sent over the network.
    /// </summary>
    /// 
    /// <remarks>
    /// Implementations must be thread safe.<br/>
    /// Implementations must be immutable.<br/>
    /// Implementations must be decorated with the <see cref="MessagePackObjectAttribute"/> attribute.<br/>
    /// Implementation members must be decorated with the <see cref="KeyAttribute"/> attribute.<br/>
    /// Any constructors may be skipped when deserializing.
    /// </remarks>
    public interface INetMessage { }


#region Internal message implementations

    [NetMessage(65000)]
    [MessagePackObject]
    public readonly struct DisconnectMessage : INetMessage
    {
        [Key(0)]
        public readonly DisconnectReason Reason;


        public DisconnectMessage(DisconnectReason reason)
        {
            Reason = reason;
        }
    }

#endregion
}