namespace Server.Networking;

public readonly struct ClientStateChangeArgs(Client client, ConnectionState newState)
{
    /// <summary>
    /// The client that changed state.
    /// </summary>
    public readonly Client Client = client;

    /// <summary>
    /// New client connection state.
    /// </summary>
    public readonly ConnectionState NewState = newState;
}