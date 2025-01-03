using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using MessagePack;
using ScaleNet.Common.LowLevel;
using ScaleNet.Common.Utils;

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

    /// <summary>
    /// A network message in packet format.
    /// Contains the message ID and the message data.<br/>
    /// Must be explicitly disposed to return the internal memory buffer to the pool.
    /// </summary>
    public readonly struct NetMessagePacket : IDisposable
    {
        public readonly byte[] Buffer;
        public readonly int Offset;
        public readonly int Length;
        public readonly bool RequireDispose;


        private NetMessagePacket(byte[] data, int offset, int length, bool requireDispose)
        {
            Buffer = data;
            Offset = offset;
            Length = length;
            RequireDispose = requireDispose;
        }
        

        /// <summary>
        /// Constructs a new outgoing network message packet.
        /// All data is copied internally, and the returned packet MUST be disposed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetMessagePacket CreateOutgoing(ushort id, byte[] data)
        {
            // Get a pooled buffer to add the message id.
            int payloadLength = data.Length;
            int packetLength = payloadLength + 2;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(packetLength);
            
            // Add the 16-bit message type ID.
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, id);
            
            // Copy the message data to the buffer.
            data.CopyTo(buffer.AsSpan(2));
            
            return new NetMessagePacket(buffer, 0, packetLength, true);
        }
        
        
        /// <summary>
        /// Constructs a new incoming network message packet.
        /// All data is copied internally, and the returned packet MUST be disposed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetMessagePacket CreateIncoming(byte[] data, int offset, int length)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            System.Buffer.BlockCopy(data, offset, buffer, 0, length);
            
            return new NetMessagePacket(buffer, 0, length, true);
        }
        
        
        /// <summary>
        /// Constructs a new incoming network message packet.
        /// The data is NOT copied internally.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetMessagePacket CreateIncomingNoCopy(byte[] data, int offset, int length, bool requireDispose)
        {
            return new NetMessagePacket(data, offset, length, requireDispose);
        }
        
        
        /// <summary>
        /// Constructs a new incoming network message packet.
        /// The data is NOT copied internally.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NetMessagePacket CreateIncomingNoCopy(ArraySegment<byte> data, bool requireDispose)
        {
            return new NetMessagePacket(data.Array!, data.Offset, data.Count, requireDispose);
        }
        
        
        public ushort ReadId()
        {
            Debug.Assert(Length >= 2, "Length >= 2");
            return BinaryPrimitives.ReadUInt16LittleEndian(Buffer.AsSpan(Offset));
        }
        
        
        public ReadOnlyMemory<byte> ReadPayload()
        {
            Debug.Assert(Length >= 2, "Length >= 2");
            return new ReadOnlyMemory<byte>(Buffer, Offset + 2, Length - 2);
        }


        public Span<byte> AsSpan() => Buffer.AsSpan(Offset, Length);
        
        
        public void Dispose()
        {
            // Safeguard, if the user calls Dispose on a packet that doesn't require it.
            if (!RequireDispose)
                return;
            
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }

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


#region Initialization

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

#endregion


        public static bool TryGetMessageType(ushort id, out Type type)
        {
            return MessageTypes.TryGetValue(id, out type);
        }
        
        
        public static bool TryGetMessageId(Type type, out ushort id)
        {
            return MessageIds.TryGetValue(type, out id);
        }


#region Serialization

        /// <summary>
        /// Serializes a network message to a packet.
        /// Internally adds a type ID header.
        /// </summary>
        /// <param name="msg">The message to serialize.</param>
        /// <param name="packet">The resulting packet, if successful. Must be disposed of after use.</param>
        public static bool TrySerialize<T>(T msg, out NetMessagePacket packet)
        {
            Debug.Assert(msg != null, nameof(msg) + " != null");
            
            if (!TryGetMessageId(msg.GetType(), out ushort id))
            {
                ScaleNetManager.Logger.LogError($"Cannot serialize: failed to get the ID of message {msg.GetType()}.");
                packet = default;
                return false;
            }
            
            //TODO: Optimize to use a buffer pool, and return a segment or Memory<byte>.
            // May require changing the internal packet format of transports to cache the packet contents internally.
            byte[] payload = MessagePackSerializer.Serialize(msg);
            
            packet = NetMessagePacket.CreateOutgoing(id, payload);
            return true;
        }


        /// <summary>
        /// Deserializes a network message from a byte array.
        /// </summary>
        public static bool TryDeserialize(in NetMessagePacket packet, out DeserializedNetMessage message)
        {
            ushort id = packet.ReadId();
            ReadOnlyMemory<byte> payload = packet.ReadPayload();
            
            if (!TryGetMessageType(id, out Type type))
            {
                ScaleNetManager.Logger.LogError($"Failed to deserialize message with ID {id}: No message type found.");
                message = default;
                return false;
            }
            
            try
            {
                object? msg = MessagePackSerializer.Deserialize(type, payload);
                Debug.Assert(msg != null, "msg != null");
                
                INetMessage? netMsg = msg as INetMessage;
                Debug.Assert(netMsg != null, "netMsg != null");

                message = new DeserializedNetMessage(netMsg, type);
                return true;
            }
            catch (Exception e)
            {
                ScaleNetManager.Logger.LogError($"Failed to deserialize message {type}: {payload.AsStringBits()}:\n{e}");
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
    public readonly struct InternalDisconnectMessage : INetMessage
    {
        [Key(0)]
        public readonly InternalDisconnectReason Reason;


        public InternalDisconnectMessage(InternalDisconnectReason reason)
        {
            Reason = reason;
        }
    }

    [NetMessage(65001)]
    [MessagePackObject]
    public readonly struct InternalPingMessage : INetMessage
    {
        // Empty message to conserve bandwidth.
    }

    [NetMessage(65002)]
    [MessagePackObject]
    public readonly struct InternalPongMessage : INetMessage
    {
        // Empty message to conserve bandwidth.
    }

#endregion
}