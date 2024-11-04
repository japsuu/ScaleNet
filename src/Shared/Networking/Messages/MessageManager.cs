using System.Reflection;

namespace Shared.Networking.Messages;

public static class MessageManager
{
    /// <summary>
    /// Uses reflection to find all NetMessage types, and registers them.
    /// </summary>
    public static void RegisterAllMessages()
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        
        // Sort the assemblies by name to ensure consistent message IDs.
        Array.Sort(assemblies, (a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
        
        foreach (Assembly assembly in assemblies)
        {
            Type[] types = assembly.GetTypes();
            
            // Sort the types by name to ensure consistent message IDs.
            Array.Sort(types, (a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
            
            foreach (Type type in types)
            {
                if (type.IsSubclassOf(typeof(NetMessage)) && !type.IsAbstract)
                    NetMessages.Register(type);
            }
        }
    }


    /// <summary>
    /// Provides methods for retrieving message IDs for message types.
    /// </summary>
    public static class NetMessages
    {
        private static byte nextId;
        private static readonly Dictionary<Type, byte> MessageIds = new();
        private static readonly Dictionary<byte, Func<NetMessage>> MessageCreators = new();

        public static byte GetId<T>() => GetId(typeof(T));


        public static byte GetId(Type type)
        {
            if (MessageIds.TryGetValue(type, out byte id))
                return id;
            
            throw new InvalidOperationException($"No message ID found for type {type}.");
        }


        public static NetMessage CreateInstance(byte id)
        {
            if (MessageCreators.TryGetValue(id, out Func<NetMessage>? creator))
                return creator();

            throw new InvalidOperationException($"No message type registered for ID {id}, cannot create instance.");
        }
        
        
        internal static void Register(Type type)
        {
            if (MessageIds.TryGetValue(type, out byte id))
                return;

            if (nextId == byte.MaxValue)
                throw new InvalidOperationException("Ran out of message IDs. Cannot register more message types.");

            id = nextId++;
            MessageIds[type] = id;

            MessageCreators[id] = () => Activator.CreateInstance(type) as NetMessage ?? throw new InvalidOperationException("Failed to create message instance.");
        }
    }
}