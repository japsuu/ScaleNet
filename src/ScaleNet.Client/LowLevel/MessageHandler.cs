using ScaleNet.Networking;

namespace ScaleNet.Client.LowLevel
{
    internal abstract class MessageHandler
    {
        public abstract void RegisterAction(object action);
        public abstract void UnregisterAction(object action);
        public abstract void Invoke(INetMessage message);
    }

    /// <summary>
    /// Handles messages received from the server.
    /// </summary>
    internal class MessageHandler<T> : MessageHandler where T : INetMessage
    {
        private readonly List<Action<T>> _actions = new();

    
        public override void RegisterAction(object action)
        {
            if (action is not Action<T> tAction)
                throw new ArgumentException($"Action is not of expected type {nameof(Action<T>)}.");
        
            _actions.Add(tAction);
        }


        public override void UnregisterAction(object action)
        {
            if (action is not Action<T> tAction)
                throw new ArgumentException($"Action is not of expected type {nameof(Action<T>)}.");
        
            _actions.Remove(tAction);
        }


        public override void Invoke(INetMessage message)
        {
            if (message is not T tMessage)
                return;
        
            foreach (Action<T> handler in _actions)
                handler.Invoke(tMessage);
        }
    }
}