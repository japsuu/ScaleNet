namespace ScaleNet.Server;

public readonly struct ClientStateChangeArgs<TConnection>(TConnection connection, ConnectionState newState)
{
    /// <summary>
    /// The client that changed state.
    /// </summary>
    public readonly TConnection Connection = connection;

    /// <summary>
    /// New client connection state.
    /// </summary>
    public readonly ConnectionState NewState = newState;
}