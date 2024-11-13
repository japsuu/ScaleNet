using Server.Networking.HighLevel;

namespace Server.Networking;

public readonly struct ClientStateArgs(Client client, SessionState state)
{
    /// <summary>
    /// The client that changed state.
    /// </summary>
    public readonly Client Client = client;

    /// <summary>
    /// New client connection state.
    /// </summary>
    public readonly SessionState State = state;
}