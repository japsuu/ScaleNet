using NetStack.Serialization;

namespace Shared.Networking.Messages;

/// <summary>
/// Represents a message (packet) that can be sent over the network.<br/>
/// </summary>
/// 
/// <remarks>
/// Implementations must be thread safe.<br/>
/// Any constructors will be skipped when deserializing, so don't rely on them being called.
/// </remarks>
public interface INetMessage
{
    public void Serialize(BitBuffer buffer);
    public MessageDeserializeResult Deserialize(BitBuffer buffer);
}