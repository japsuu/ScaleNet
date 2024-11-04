using Shared.Networking.Messages;

namespace Client.Networking;

public abstract class MessageHandlerCollection
{
    public abstract void RegisterHandler(object handler);
    public abstract void UnregisterHandler(object handler);
    public abstract void InvokeHandlers(NetMessage message);
}

/// <summary>
/// Handles packets received on clients, from the server.
/// </summary>
public class MessageHandlerCollection<T> : MessageHandlerCollection where T : NetMessage
{
    private readonly List<Action<T>> _handlers = [];

    
    public override void RegisterHandler(object handler)
    {
        if (handler is not Action<T> tHandler)
            throw new ArgumentException("Handler is not of type Action<T>.");
        
        _handlers.Add(tHandler);
    }


    public override void UnregisterHandler(object handler)
    {
        if (handler is not Action<T> tHandler)
            throw new ArgumentException("Handler is not of type Action<T>.");
        
        _handlers.Remove(tHandler);
    }


    public override void InvokeHandlers(NetMessage message)
    {
        if (message is not T tMessage)
            return;
        
        foreach (Action<T> handler in _handlers)
            handler.Invoke(tMessage);
    }
}