using MessagePack;

namespace Shared.Networking.Messages;

public static class NetMessages
{
    public static byte[] Serialize(INetMessage msg)
    {
        byte[] bin = MessagePackSerializer.Serialize(msg);
        
        return bin;
    }


    public static INetMessage Deserialize(ReadOnlyMemory<byte> bin)
    {
        INetMessage msg = MessagePackSerializer.Deserialize<INetMessage>(bin);

        return msg;
    }
}

/// <summary>
/// Represents a message (packet) that can be sent over the network.<br/>
/// </summary>
/// 
/// <remarks>
/// Implementations must be thread safe.<br/>
/// Any constructors will be skipped when deserializing, so don't rely on them being called.
/// </remarks>
[Union(0, typeof(AuthRequestMessage))]
[Union(1, typeof(AuthResponseMessage))]
[Union(2, typeof(DisconnectMessage))]
[Union(3, typeof(SessionInitiateMessage))]
[Union(4, typeof(WelcomeMessage))]
[Union(5, typeof(ChatMessage))]
[Union(6, typeof(ChatMessageNotification))]
public interface INetMessage;

/// <summary>
/// Sent by the server to request authentication from the client.
/// </summary>
///
/// <remarks>
/// Server -&gt; Client
/// </remarks>
[MessagePackObject]
public readonly struct AuthRequestMessage(AuthenticationMethod authenticationMethod) : INetMessage
{
    [Key(0)]
    public readonly AuthenticationMethod AuthenticationMethod = authenticationMethod;
}

/// <summary>
/// Sent by the client in response to a <see cref="AuthRequestMessage"/>.
/// </summary>
///
/// <remarks>
/// Client -&gt; Server
/// </remarks>
[MessagePackObject]
public readonly struct AuthResponseMessage(string username, string password) : INetMessage
{
    [Key(0)]
    public readonly string Username = username;
    [Key(1)]
    public readonly string Password = password;
}

/// <summary>
/// Sent by either the server or the client.
/// Contains a reason for the disconnection.
/// </summary>
///
/// <remarks>
/// Server &lt;-&gt; Client
/// </remarks>
[MessagePackObject]
public readonly struct DisconnectMessage(DisconnectReason reason) : INetMessage
{
    [Key(0)]
    public readonly DisconnectReason Reason = reason;
}

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
[MessagePackObject]
public readonly struct SessionInitiateMessage(ushort version) : INetMessage
{
    [Key(0)]
    public readonly ushort Version = version;
}

/// <summary>
/// Sent from the server to the client,
/// if the client is accepted as a valid connection.<br/>
/// Contains the client's sessionId.
/// </summary>
///
/// <remarks>
/// Server -&gt; Client
/// </remarks>
[MessagePackObject]
public readonly struct WelcomeMessage(uint sessionId) : INetMessage
{
    [Key(0)]
    public readonly uint SessionId = sessionId;
}

/// <summary>
/// Sent from the client to the server,
/// when the client wants to send a chat message.
/// </summary>
///
/// <remarks>
/// Client -&gt; Server
/// </remarks>
[MessagePackObject]
public readonly struct ChatMessage(string message) : INetMessage
{
    [Key(0)]
    public readonly string Message = message;
}

/// <summary>
/// Sent from the server to the client,
/// when the server wants to notify the client about a chat message.
/// </summary>
///
/// <remarks>
/// Server -&gt; Client
/// </remarks>
[MessagePackObject]
public readonly struct ChatMessageNotification(string user, string message) : INetMessage
{
    [Key(0)]
    public readonly string User = user;
    
    [Key(1)]
    public readonly string Message = message;
}