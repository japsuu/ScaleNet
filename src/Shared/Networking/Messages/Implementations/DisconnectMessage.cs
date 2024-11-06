using NetStack.Serialization;

namespace Shared.Networking.Messages;

/// <summary>
/// Sent by either the server or the client.
/// Contains a reason for the disconnection.
/// </summary>
///
/// <remarks>
/// Server &lt;-&gt; Client
/// </remarks>
public class DisconnectMessage(DisconnectReason reason) : NetMessage
{
    public DisconnectReason Reason { get; private set; } = reason;


    protected override void SerializeInternal(BitBuffer buffer)
    {
        buffer.AddByte((byte)Reason);
    }


    protected override MessageDeserializeResult DeserializeInternal(BitBuffer buffer)
    {
        Reason = (DisconnectReason)buffer.ReadByte();
        
        return MessageDeserializeResult.Success;
    }
}