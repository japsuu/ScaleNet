namespace ScaleNet.Client
{
    public readonly struct ConnectionStateArgs
    {
        public readonly ConnectionState NewConnectionState;


        public ConnectionStateArgs(ConnectionState newConnectionState)
        {
            NewConnectionState = newConnectionState;
        }
    }
}