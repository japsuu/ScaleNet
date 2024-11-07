namespace Shared.Networking.Messages;

/// <summary>
/// Sent from the server to the client,
/// if the client is accepted as a valid connection.<br/>
/// Contains the client's sessionId.
/// </summary>
///
/// <remarks>
/// Server -&gt; Client
/// </remarks>
public readonly struct WelcomeMessage(SessionId sessionId) : INetMessage
{
    public readonly SessionId SessionId = sessionId;
}