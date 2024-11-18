namespace ScaleNet.Client;

public readonly struct ConnectionStateArgs(ConnectionState newConnectionState, ConnectionState oldConnectionState)
{
    public readonly ConnectionState NewConnectionState = newConnectionState;
    public readonly ConnectionState OldConnectionState = oldConnectionState;
}