using NetStack.Serialization;

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
public class WelcomeMessage(SessionId sessionId) : NetMessage
{
    public SessionId SessionId { get; private set; } = sessionId;


    protected override void SerializeInternal(BitBuffer buffer)
    {
        buffer.AddUInt(SessionId.Value);
    }


    protected override MessageDeserializeResult DeserializeInternal(BitBuffer buffer)
    {
        SessionId = new SessionId(buffer.ReadUInt());
        return MessageDeserializeResult.Success;
    }
}