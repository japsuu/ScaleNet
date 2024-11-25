namespace ScaleNet.Client
{
    /// <summary>
    /// States a network connection can be in.
    /// </summary>
    public enum ConnectionState : byte
    {
        /// <summary>
        /// Connection is fully stopped.
        /// </summary>
        Disconnected = 0,

        /// <summary>
        /// Connection is established.
        /// </summary>
        Connected = 1,
    }
}