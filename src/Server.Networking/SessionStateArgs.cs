using Server.Networking.HighLevel;

namespace Server.Networking;

public readonly struct SessionStateArgs(SessionId sessionId, SessionState state)
{
    /// <summary>
    /// The ID of the client that changed state.
    /// </summary>
    public readonly SessionId SessionId = sessionId;

    /// <summary>
    /// New client connection state.
    /// </summary>
    public readonly SessionState State = state;
}