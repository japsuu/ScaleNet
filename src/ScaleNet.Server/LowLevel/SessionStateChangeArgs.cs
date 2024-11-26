namespace ScaleNet.Server.LowLevel;

public readonly struct SessionStateChangeArgs(Guid sessionId, ConnectionState newState)
{
    /// <summary>
    /// The ID of the client that changed state.
    /// </summary>
    public readonly Guid SessionId = sessionId;

    /// <summary>
    /// New client connection state.
    /// </summary>
    public readonly ConnectionState NewState = newState;
}