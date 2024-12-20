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
    /// Connection is established.<br/>
    /// Connection is encrypted and ready to send and receive data.
    /// </summary>
    Connected = 1,
}