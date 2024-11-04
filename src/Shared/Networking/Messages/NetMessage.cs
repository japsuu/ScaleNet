using NetStack.Serialization;

namespace Shared.Networking.Messages;

/// <summary>
/// Represents a message (packet) that can be sent over the network.<br/>
/// 
/// <remarks>
/// Implementations must be thread safe.<br/>
/// You should not cache instances of this class, as it is part of a pool and will be reused.<br/>
/// Any constructors will be skipped when deserializing, so don't rely on them being called.
/// </remarks>
/// </summary>
public abstract class NetMessage
{
    public abstract byte Id { get; }


    protected NetMessage()
    {
        
    }
    
    
    public void Serialize(BitBuffer buffer)
    {
        buffer.AddByte(Id);
        SerializeInternal(buffer);
    }
    
    
    public bool Deserialize(BitBuffer buffer)
    {
        return DeserializeInternal(buffer);
    }


    protected abstract void SerializeInternal(BitBuffer buffer);
    protected abstract bool DeserializeInternal(BitBuffer buffer);


    public override string ToString()
    {
        return $"{GetType().Name} (ID: {Id})";
    }
}