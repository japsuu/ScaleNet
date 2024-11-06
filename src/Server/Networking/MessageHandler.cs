using Shared.Networking.Messages;

namespace Server.Networking;

internal abstract class MessageHandler
{
    public abstract bool RequiresAuthentication { get; }
    public abstract void RegisterAction(object action);
    public abstract void UnregisterAction(object action);
    public abstract void Invoke(PlayerSession session, NetMessage message);
}

/// <summary>
/// Handles packets received on clients, from the server.
/// </summary>
internal class MessageHandler<T>(bool requiresAuthentication) : MessageHandler where T : NetMessage
{
    private readonly List<Action<PlayerSession, T>> _actions = [];
    
    public override bool RequiresAuthentication => requiresAuthentication;

    
    /// <remarks>
    /// Not thread-safe.
    /// </remarks>
    public override void RegisterAction(object action)
    {
        if (action is not Action<PlayerSession, T> tAction)
            throw new ArgumentException($"Action is not of type {nameof(Action<PlayerSession, T>)}.");
        
        _actions.Add(tAction);
    }
    

    /// <remarks>
    /// Not thread-safe.
    /// </remarks>
    public override void UnregisterAction(object action)
    {
        if (action is not Action<PlayerSession, T> tAction)
            throw new ArgumentException($"Action is not of type {nameof(Action<PlayerSession, T>)}.");
        
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
    public override void Invoke(PlayerSession session, NetMessage message)
    {
        if (message is not T tMessage)
            return;
        
        foreach (Action<PlayerSession, T> handler in _actions)
            handler.Invoke(session, tMessage);
    }
}