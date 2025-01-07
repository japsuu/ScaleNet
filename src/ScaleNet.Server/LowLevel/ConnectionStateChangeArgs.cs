namespace ScaleNet.Server.LowLevel;

public readonly struct ConnectionStateChangeArgs(ConnectionId connectionId, ConnectionState newState)
{
    /// <summary>
    /// The ID of the client that changed state.
    /// </summary>
    public readonly ConnectionId ConnectionId = connectionId;

    /// <summary>
    /// New client connection state.
    /// </summary>
    public readonly ConnectionState NewState = newState;
}