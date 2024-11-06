namespace Server.Networking;

internal readonly struct ServerStateArgs(ServerState state)
{
    /// <summary>
    /// New server connection state.
    /// </summary>
    public readonly ServerState State = state;
}