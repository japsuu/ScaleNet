namespace Shared.Networking.Messages;

/// <summary>
/// Sent by either the server or the client.
/// Contains a reason for the disconnection.
/// </summary>
///
/// <remarks>
/// Server &lt;-&gt; Client
/// </remarks>
public readonly struct DisconnectMessage(DisconnectReason reason) : INetMessage
{
    public readonly DisconnectReason Reason = reason;
}