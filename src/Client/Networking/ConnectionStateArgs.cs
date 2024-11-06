namespace Client.Networking;

public readonly struct ConnectionStateArgs(ConnectionState connectionState)
{
    /// <summary>
    /// New connection state.
    /// </summary>
    public readonly ConnectionState ConnectionState = connectionState;
}