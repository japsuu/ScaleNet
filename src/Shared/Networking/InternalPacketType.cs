namespace Shared.Networking;

/// <summary>
/// Represents the type of data packet being sent or received.
/// </summary>
public enum InternalPacketType : byte
{
    /// <summary>
    /// Packet contains unknown data.
    /// </summary>
    Unset = 0,
    
    /// <summary>
    /// Hello packet.
    /// Sent by the client to the server to initiate the connection.
    /// Contains the client's version and name.
    ///
    /// <remarks>
    /// Client -&gt; Server
    /// </remarks>
    /// </summary>
    Hello = 1,
    
    /// <summary>
    /// Welcome packet.
    /// Contains the client's connectionId.
    /// </summary>
    ///
    /// <remarks>
    /// Server -&gt; Client
    /// </remarks>
    Welcome = 2,
    
    /// <summary>
    /// Packet contains a message.
    /// </summary>
    ///
    /// <remarks>
    /// Server &lt;-&gt; Client
    /// </remarks>
    Message = 3,
    
    /// <summary>
    /// The server is disconnecting the client.
    /// Contains a reason for the disconnection.
    /// </summary>
    ///
    /// <remarks>
    /// Server -&gt; Client
    /// </remarks>
    DisconnectNotification = 4,
    
    /// <summary>
    /// The client is requesting to disconnect from the server.
    /// </summary>
    ///
    /// <remarks>
    /// Client -&gt; Server
    /// </remarks>
    DisconnectRequest = 5,
}