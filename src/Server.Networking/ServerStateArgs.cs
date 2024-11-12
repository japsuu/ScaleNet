namespace Server.Networking;

public readonly struct ServerStateArgs(ServerState newState, ServerState oldState)
{
    public readonly ServerState NewState = newState;
    public readonly ServerState OldState = oldState;
}