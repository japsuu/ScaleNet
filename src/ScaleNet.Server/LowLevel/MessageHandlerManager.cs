using ScaleNet.Common;

namespace ScaleNet.Server.LowLevel;

/// <summary>
/// Maintains a collection of message handlers, tied to specific message types.
/// </summary>
internal class MessageHandlerManager<TConnection> where TConnection : Connection
{
    private readonly Dictionary<Type, MessageHandler> _messageHandlers = [];


    /// <summary>
    /// Registers a method to call when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">Method to call.</param>
    /// <typeparam name="T"></typeparam>
    public void RegisterMessageHandler<T>(Action<TConnection, T> handler) where T : INetMessage
    {
        Type msgType = typeof(T);
        
        if (!_messageHandlers.TryGetValue(msgType, out MessageHandler? handlerCollection))
        {
            handlerCollection = new MessageHandler<T>();
            _messageHandlers.TryAdd(msgType, handlerCollection);
        }

        handlerCollection.RegisterAction(handler);
    }


    /// <summary>
    /// Unregisters a method from being called when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">The method to unregister.</param>
    /// <typeparam name="T">Type of message to unregister.</typeparam>
    public void UnregisterMessageHandler<T>(Action<TConnection, T> handler) where T : INetMessage
    {
        Type key = typeof(T);
        
        if (_messageHandlers.TryGetValue(key, out MessageHandler? handlerCollection))
            handlerCollection.UnregisterAction(handler);
    }


    /// <summary>
    /// Tries to handle a message.
    /// </summary>
    /// <param name="connection">The client that sent the message.</param>
    /// <param name="msg">The message to handle.</param>
    /// <returns>True if the message was handled, false otherwise.</returns>
    public void TryHandleMessage(TConnection connection, DeserializedNetMessage msg)
    {
        Type msgType = msg.Type;
        
        // Try to get a handler.
        if (!_messageHandlers.TryGetValue(msgType, out MessageHandler? messageHandler))
        {
            Networking.Logger.LogWarning($"No handler is registered for {msgType}. Ignoring.");
            return;
        }

        // Invoke handler with message.
        messageHandler.Invoke(connection, msg.Message);
    }
}