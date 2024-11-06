using Server.Networking.LowLevel;

namespace Server.Networking;

internal readonly struct ClientStateArgs(ClientConnection connection, ClientState state)
{
    /// <summary>
    /// The client connection that changed state.
    /// </summary>
    public readonly ClientConnection Connection = connection;

    /// <summary>
    /// New client connection state.
    /// </summary>
    public readonly ClientState State = state;
}