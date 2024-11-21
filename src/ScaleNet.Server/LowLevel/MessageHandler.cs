using ScaleNet.Common;

namespace ScaleNet.Server.LowLevel;

internal abstract class MessageHandler
{
    public abstract void RegisterAction(object action);
    public abstract void UnregisterAction(object action);
    public abstract void Invoke(Connection session, INetMessage message);
}

/// <summary>
/// Handles packets received on clients, from the server.
/// </summary>
internal class MessageHandler<T> : MessageHandler where T : INetMessage
{
    private readonly List<Action<Connection, T>> _actions = [];

    
    /// <remarks>
    /// Not thread-safe.
    /// </remarks>
    public override void RegisterAction(object action)
    {
        if (action is not Action<Connection, T> tAction)
            throw new ArgumentException($"Action is not of type {nameof(Action<Connection, T>)}.");
        
        _actions.Add(tAction);
    }
    

    /// <remarks>
    /// Not thread-safe.
    /// </remarks>
    public override void UnregisterAction(object action)
    {
        if (action is not Action<Connection, T> tAction)
            throw new ArgumentException($"Action is not of type {nameof(Action<Connection, T>)}.");
        
        _actions.Remove(tAction);
    }


    /// <summary>
    /// Calls all registered actions.
    /// </summary>
    /// <param name="session"></param>
    /// <param name="message"></param>
    /// <remarks>
    /// Not thread-safe.
    /// </remarks>
    public override void Invoke(Connection session, INetMessage message)
    {
        if (message is not T tMessage)
            return;
        
        foreach (Action<Connection, T> handler in _actions)
            handler.Invoke(session, tMessage);
    }
}