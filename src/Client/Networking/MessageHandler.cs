using Shared.Networking.Messages;

namespace Client.Networking;

public abstract class MessageHandler
{
    public abstract void RegisterAction(object action);
    public abstract void UnregisterAction(object action);
    public abstract void Invoke(NetMessage message);
}

/// <summary>
/// Handles packets received on clients, from the server.
/// </summary>
public class MessageHandler<T> : MessageHandler where T : NetMessage
{
    private readonly List<Action<T>> _actions = [];

    
    public override void RegisterAction(object action)
    {
        if (action is not Action<T> tAction)
            throw new ArgumentException("Action is not of type Action<T>.");
        
        _actions.Add(tAction);
    }


    public override void UnregisterAction(object action)
    {
        if (action is not Action<T> tAction)
            throw new ArgumentException("Action is not of type Action<T>.");
        
        _actions.Remove(tAction);
    }


    public override void Invoke(NetMessage message)
    {
        if (message is not T tMessage)
            return;
        
        foreach (Action<T> handler in _actions)
            handler.Invoke(tMessage);
    }
}