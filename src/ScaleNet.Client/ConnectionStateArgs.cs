namespace ScaleNet.Client
{
    public readonly struct ConnectionStateArgs
    {
        public readonly ConnectionState NewConnectionState;
        public readonly ConnectionState OldConnectionState;


        public ConnectionStateArgs(ConnectionState newConnectionState, ConnectionState oldConnectionState)
        {
            NewConnectionState = newConnectionState;
            OldConnectionState = oldConnectionState;
        }
    }
}