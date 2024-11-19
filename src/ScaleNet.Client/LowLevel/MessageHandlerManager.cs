using System;
using System.Collections.Generic;
using ScaleNet.Networking;
using ScaleNet.Utils;

namespace ScaleNet.Client.LowLevel
{
    /// <summary>
    /// Maintains a collection of message handlers, tied to specific message types.
    /// </summary>
    public class MessageHandlerManager
    {
        private readonly Dictionary<Type, MessageHandler> _messageHandlers = new();


        /// <summary>
        /// Registers a message handler for the specified message type.
        /// </summary>
        public void RegisterMessageHandler<T>(Action<T> handler) where T : INetMessage
        {
            Type key = typeof(T);

            if (!_messageHandlers.TryGetValue(key, out MessageHandler? handlerCollection))
            {
                handlerCollection = new MessageHandler<T>();
                _messageHandlers.Add(key, handlerCollection);
            }

            handlerCollection.RegisterAction(handler);
        }


        /// <summary>
        /// Unregisters a message handler for the specified message type.
        /// </summary>
        public void UnregisterMessageHandler<T>(Action<T> handler) where T : INetMessage
        {
            Type key = typeof(T);
        
            if (_messageHandlers.TryGetValue(key, out MessageHandler? handlerCollection))
                handlerCollection.UnregisterAction(handler);
        }


        /// <summary>
        /// Tries to handle a message.
        /// </summary>
        /// <param name="msg">The message to handle.</param>
        /// <returns>True if the message was handled, false otherwise.</returns>
        public void TryHandleMessage(DeserializedNetMessage msg)
        {
            Type messageId = msg.Type;
        
            // Try to get a handler.
            if (!_messageHandlers.TryGetValue(messageId, out MessageHandler? packetHandler))
            {
                Logger.LogWarning($"No handler is registered for {msg}. Ignoring.");
                return;
            }

            // Invoke handler with message.
            packetHandler.Invoke(msg.Message);
        }
    }
}