using ScaleNet.Common;

namespace ScaleNet.Server.LowLevel;

internal abstract class MessageHandler<TConnection>
{
    public abstract void RegisterAction(object action);
    public abstract void UnregisterAction(object action);
    public abstract void Invoke(TConnection connection, INetMessage message);
}

/// <summary>
/// Handles packets received on clients, from the server.
/// </summary>
internal class MessageHandler<TConnection, T> : MessageHandler<TConnection> where T : INetMessage
{
    private readonly List<Action<TConnection, T>> _actions = [];

    
    /// <remarks>
    /// Not thread-safe.
    /// </remarks>
    public override void RegisterAction(object action)
    {
        if (action is not Action<TConnection, T> tAction)
            throw new ArgumentException($"Action is not of type {nameof(Action<TConnection, T>)}.");
        
        _actions.Add(tAction);
    }
    

    /// <remarks>
    /// Not thread-safe.
    /// </remarks>
    public override void UnregisterAction(object action)
    {
        if (action is not Action<TConnection, T> tAction)
            throw new ArgumentException($"Action is not of type {nameof(Action<TConnection, T>)}.");
        
        _actions.Remove(tAction);
    }


    /// <summary>
    /// Calls all registered actions.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="message"></param>
    /// <remarks>
    /// Not thread-safe.
    /// </remarks>
    public override void Invoke(TConnection connection, INetMessage message)
    {
        if (message is not T tMessage)
            return;
        
        foreach (Action<TConnection, T> handler in _actions)
            handler.Invoke(connection, tMessage);
    }
}