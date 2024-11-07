namespace Shared.Networking.Messages;

/// <summary>
/// Sent by the server to request authentication from the client.
/// </summary>
///
/// <remarks>
/// Server -&gt; Client
/// </remarks>
public readonly struct AuthRequestMessage(AuthenticationMethod authenticationMethod) : INetMessage
{
    public readonly AuthenticationMethod AuthenticationMethod = authenticationMethod;
}