namespace Server.Networking;

/// <summary>
/// States a server can be in.
/// </summary>
public enum ServerState : byte
{
    /// <summary>
    /// Connection is fully stopped.
    /// </summary>
    Stopped = 0,

    /// <summary>
    /// Connection is starting but not yet established.
    /// </summary>
    Starting = 1,

    /// <summary>
    /// Connection is established.
    /// </summary>
    Started = 2,

    /// <summary>
    /// Connection is stopping.
    /// </summary>
    Stopping = 3
}