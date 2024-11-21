namespace ScaleNet.Common
{
    /// <summary>
    /// The reason for a client's disconnection.
    /// </summary>
    public enum DisconnectReason : byte
    {
        /// <summary>
        /// No reason was specified.
        /// </summary>
        None,
    
        /// <summary>
        /// Client performed an action which could only be done if trying to exploit the server.
        /// </summary>
        ExploitAttempt,
    
        /// <summary>
        /// Data received from the client could not be parsed. This rarely indicates an attack.
        /// </summary>
        MalformedData,
    
        /// <summary>
        /// There was a problem with the server that required the client to be kicked.
        /// </summary>
        UnexpectedProblem,
    
        ServerShutdown,
        OversizedPacket,
        CorruptPlayerData,
        TooManyPackets
    }
}