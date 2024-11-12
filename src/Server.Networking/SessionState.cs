namespace Server.Networking;

/// <summary>
/// States a remove network connection can be in.
/// </summary>
public enum SessionState : byte
{
    /// <summary>
    /// Connection is fully stopped.
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// Connection is starting but not yet established.
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// Connection is established.
    /// </summary>
    Connected = 2,

    /// <summary>
    /// Connection is stopping.
    /// </summary>
    Disconnecting = 3
}