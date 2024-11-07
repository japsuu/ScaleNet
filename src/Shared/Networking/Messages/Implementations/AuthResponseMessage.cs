namespace Shared.Networking.Messages;

/// <summary>
/// Sent by the client in response to a <see cref="AuthRequestMessage"/>.
/// </summary>
///
/// <remarks>
/// Client -&gt; Server
/// </remarks>
public readonly struct AuthResponseMessage(string username, string password) : INetMessage
{
    public readonly string Username = username;
    public readonly string Password = password;
}