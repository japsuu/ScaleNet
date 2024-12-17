namespace ScaleNet.Common.LowLevel
{
    /// <summary>
    /// The internal reason for a client's disconnection.
    /// </summary>
    public enum InternalDisconnectReason : byte
    {
        /// <summary>
        /// No reason was specified.
        /// </summary>
        None,
    
        /// <summary>
        /// Data received from the client could not be parsed. This rarely indicates an attack.
        /// </summary>
        MalformedData,
    
        /// <summary>
        /// There was a problem with the server that required the client to be kicked.
        /// </summary>
        UnexpectedProblem,
    
        /// <summary>
        /// The server is shutting down.
        /// </summary>
        ServerShutdown,
        
        /// <summary>
        /// The client sent an oversized packet.
        /// </summary>
        OversizedPacket,
        
        /// <summary>
        /// The client sent too many packets in a short period of time.
        /// </summary>
        TooManyPackets,
        
        /// <summary>
        /// User defined reason, a message with further details should have been sent.
        /// </summary>
        User,
    }
}