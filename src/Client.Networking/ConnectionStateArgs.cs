namespace Client.Networking;

public readonly struct ConnectionStateArgs(ConnectionState newConnectionState, ConnectionState oldConnectionState)
{
    public readonly ConnectionState NewConnectionState = newConnectionState;
    public readonly ConnectionState OldConnectionState = oldConnectionState;
}