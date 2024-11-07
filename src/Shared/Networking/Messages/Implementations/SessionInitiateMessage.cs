namespace Shared.Networking.Messages;

/// <summary>
/// Sent from the client to the server,
/// when initiating a connection.<br/>
/// Contains the client's version.<br/>
/// If the connection is accepted, server responds with <see cref="WelcomeMessage"/>.
/// If authentication is required, server responds with <see cref="AuthRequestMessage"/>.
/// </summary>
///
/// <remarks>
/// Client -&gt; Server
/// </remarks>
public readonly struct SessionInitiateMessage(ushort version) : INetMessage
{
    public readonly ushort Version = version;
}