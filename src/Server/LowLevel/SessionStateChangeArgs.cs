namespace ScaleNet.Server.LowLevel;

public readonly struct SessionStateChangeArgs(SessionId sessionId, ConnectionState newState)
{
    /// <summary>
    /// The ID of the client that changed state.
    /// </summary>
    public readonly SessionId SessionId = sessionId;

    /// <summary>
    /// New client connection state.
    /// </summary>
    public readonly ConnectionState NewState = newState;
}