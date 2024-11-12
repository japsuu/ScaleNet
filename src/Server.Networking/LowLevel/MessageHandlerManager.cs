using Server.Networking.HighLevel;
using Shared.Networking.Messages;

namespace Server.Networking.LowLevel;

/// <summary>
/// Maintains a collection of message handlers, tied to specific message types.
/// </summary>
internal class MessageHandlerManager
{
    private readonly Dictionary<Type, MessageHandler> _messageHandlers = [];


    /// <summary>
    /// Registers a method to call when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">Method to call.</param>
    /// <typeparam name="T"></typeparam>
    /// <param name="requiresAuthentication">True if the client must be authenticated to send this message.</param>
    public void RegisterMessageHandler<T>(Action<Client, T> handler, bool requiresAuthentication = true) where T : INetMessage
    {
        Type key = typeof(T);
        
        if (!_messageHandlers.TryGetValue(key, out MessageHandler? handlerCollection))
        {
            handlerCollection = new MessageHandler<T>(requiresAuthentication);
            _messageHandlers.TryAdd(key, handlerCollection);
        }

        handlerCollection.RegisterAction(handler);
    }


    /// <summary>
    /// Unregisters a method from being called when a message of the specified type arrives.
    /// </summary>
    /// <param name="handler">The method to unregister.</param>
    /// <typeparam name="T">Type of message to unregister.</typeparam>
    public void UnregisterMessageHandler<T>(Action<Client, T> handler) where T : INetMessage
    {
        Type key = typeof(T);
        
        if (_messageHandlers.TryGetValue(key, out MessageHandler? handlerCollection))
            handlerCollection.UnregisterAction(handler);
    }


    /// <summary>
    /// Tries to handle a message.
    /// </summary>
    /// <param name="client">The client that sent the message.</param>
    /// <param name="msg">The message to handle.</param>
    /// <returns>True if the message was handled, false otherwise.</returns>
    public bool TryHandleMessage(Client client, INetMessage msg)
    {
        Type messageId = msg.GetType();
        
        // Try to get a handler.
        if (!_messageHandlers.TryGetValue(messageId, out MessageHandler? packetHandler))
            return false;

        // Invoke handler with message.
        packetHandler.Invoke(client, msg);
        return true;
    }
}