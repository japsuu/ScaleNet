namespace ScaleNet.Server;

/// <summary>
/// States a remove network connection can be in.
/// </summary>
public enum ConnectionState : byte
{
    /// <summary>
    /// Connection is fully stopped.
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// Connection is starting but not yet established.<br/>
    /// Not encrypted yet.
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// Connection is established.<br/>
    /// Not encrypted yet.
    /// </summary>
    Connected = 2,
    
    /// <summary>
    /// Connection is in the process of SSL handshaking.<br/>
    /// Not encrypted yet.
    /// </summary>
    SslHandshaking = 3,

    /// <summary>
    /// Connection has completed the SSL handshake and is ready to send and receive data.<br/>
    /// Encrypted.
    /// </summary>
    Ready = 4,

    /// <summary>
    /// Connection is stopping.<br/>
    /// Encrypted.
    /// </summary>
    Disconnecting = 5
}