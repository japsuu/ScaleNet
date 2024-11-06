using NetStack.Serialization;

namespace Shared.Networking.Messages;

/// <summary>
/// Represents a message (packet) that can be sent over the network.<br/>
/// </summary>
/// 
/// <remarks>
/// Implementations must be thread safe.<br/>
/// You should not cache instances of this class, as it is part of a pool and will be reused.<br/>
/// Any constructors will be skipped when deserializing, so don't rely on them being called.
/// </remarks>
public abstract class NetMessage
{
    public void Serialize(BitBuffer buffer)
    {
        buffer.AddByte(MessageManager.NetMessages.GetId(GetType()));
        SerializeInternal(buffer);
    }
    
    
    public MessageDeserializeResult Deserialize(BitBuffer buffer)
    {
        return DeserializeInternal(buffer);
    }


    protected abstract void SerializeInternal(BitBuffer buffer);
    protected abstract MessageDeserializeResult DeserializeInternal(BitBuffer buffer);


    public override string ToString()
    {
        return $"{GetType().Name} (ID: {MessageManager.NetMessages.GetId(GetType())})";
    }
}