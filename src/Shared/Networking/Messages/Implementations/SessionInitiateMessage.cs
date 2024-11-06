using NetStack.Serialization;

namespace Shared.Networking.Messages;

/// <summary>
/// Sent from the client to the server,
/// when initiating a connection.<br/>
/// Contains the client's credentials.<br/>
/// If the connection is accepted, server responds with <see cref="WelcomeMessage"/>.
/// </summary>
///
/// <remarks>
/// Client -&gt; Server
/// </remarks>
public class SessionInitiateMessage(ushort version, string username) : NetMessage
{
    public ushort Version { get; private set; } = version;
    public string Username { get; private set; } = username;


    protected override void SerializeInternal(BitBuffer buffer)
    {
        buffer.AddUShort(Version);
        buffer.AddString(Username);
    }


    protected override MessageDeserializeResult DeserializeInternal(BitBuffer buffer)
    {
        Version = buffer.ReadUShort();
        
        if (Version != SharedConstants.GAME_VERSION)
            return MessageDeserializeResult.OutdatedVersion;
        
        Username = buffer.ReadString();
        
        if (string.IsNullOrEmpty(Username))
            return MessageDeserializeResult.MalformedData;
        
        return MessageDeserializeResult.Success;
    }
}