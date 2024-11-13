namespace Server.Networking;

public readonly struct ServerStateChangeArgs(ServerState newState, ServerState oldState)
{
    public readonly ServerState NewState = newState;
    public readonly ServerState OldState = oldState;
}