using NetStack.Serialization;

namespace Shared.Networking.Messages;

/// <summary>
/// Sent by the server to request authentication from the client.
/// </summary>
///
/// <remarks>
/// Server -&gt; Client
/// </remarks>
public class AuthRequestMessage(AuthenticationMethod authenticationMethod) : NetMessage
{
    public AuthenticationMethod AuthenticationMethod { get; private set; } = authenticationMethod;


    protected override void SerializeInternal(BitBuffer buffer)
    {
        buffer.AddByte((byte)AuthenticationMethod);
    }


    protected override MessageDeserializeResult DeserializeInternal(BitBuffer buffer)
    {
        AuthenticationMethod = (AuthenticationMethod)buffer.ReadByte();
        
        return MessageDeserializeResult.Success;
    }


    public override string ToString()
    {
        return $"{GetType().Name} (ID: {MessageManager.NetMessages.GetId(GetType())})";
    }
}