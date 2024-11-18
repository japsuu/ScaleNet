using MessagePack;
using ScaleNet.Utils;

namespace ScaleNet.Networking;

public static class NetMessages
{
    public static byte[] Serialize<T>(T msg) where T : INetMessage
    {
        byte[] bin = MessagePackSerializer.Serialize<INetMessage>(msg);
        
        return bin;
    }


    public static INetMessage? Deserialize(ReadOnlyMemory<byte> bin)
    {
        try
        {
            INetMessage msg = MessagePackSerializer.Deserialize<INetMessage>(bin);

            return msg;
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to deserialize message: {bin.AsStringBits()}:\n{e}");
            return null;
        }
    }
}

/// <summary>
/// Represents a message (packet) that can be sent over the network.<br/>
/// </summary>
/// 
/// <remarks>
/// Implementations must be thread safe.<br/>
/// Implementations must be immutable.<br/>
/// Any constructors may be skipped when deserializing.
/// </remarks>

// Framework messages
[Union(0, typeof(DisconnectMessage))]
[Union(1, typeof(AuthenticationInfoMessage))]
[Union(2, typeof(RegisterRequestMessage))]
[Union(3, typeof(RegisterResponseMessage))]
[Union(4, typeof(AuthenticationRequestMessage))]
[Union(5, typeof(AuthenticationResponseMessage))]

// Application messages
[Union(64, typeof(ChatMessage))]
[Union(65, typeof(ChatMessageNotification))]
public interface INetMessage;


[MessagePackObject]
public readonly struct DisconnectMessage(DisconnectReason reason) : INetMessage
{
    [Key(0)]
    public readonly DisconnectReason Reason = reason;
}


[MessagePackObject]
public readonly struct AuthenticationInfoMessage(bool registrationAllowed, uint serverVersion) : INetMessage
{
    [Key(0)]
    public readonly bool RegistrationAllowed = registrationAllowed;
    
    [Key(1)]
    public readonly uint ServerVersion = serverVersion;
}


#region Account register

[MessagePackObject]
public readonly struct RegisterRequestMessage(string username, string password) : INetMessage
{
    [Key(0)]
    public readonly string Username = username;
    
    [Key(1)]
    public readonly string Password = password;
}

[MessagePackObject]
public readonly struct RegisterResponseMessage(AccountCreationResult result) : INetMessage
{
    [Key(0)]
    public readonly AccountCreationResult Result = result;
}

#endregion


#region Account authentication

[MessagePackObject]
public readonly struct AuthenticationRequestMessage(string username, string password) : INetMessage
{
    [Key(0)]
    public readonly string Username = username;
    
    [Key(1)]
    public readonly string Password = password;
}

[MessagePackObject]
public readonly struct AuthenticationResponseMessage(AuthenticationResult result, uint clientUid) : INetMessage
{
    [Key(0)]
    public readonly AuthenticationResult Result = result;
    
    [Key(1)]
    public readonly uint ClientUid = clientUid;
}

#endregion


#region Chat

[MessagePackObject]
public readonly struct ChatMessage(string message) : INetMessage
{
    [Key(0)]
    public readonly string Message = message;
}

[MessagePackObject]
public readonly struct ChatMessageNotification(string user, string message) : INetMessage
{
    [Key(0)]
    public readonly string User = user;
    
    [Key(1)]
    public readonly string Message = message;
}

#endregion

/*/// <summary>
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
public readonly struct AuthResponseMessage(string username, string password, ushort version) : INetMessage
{
    [Key(0)]
    public readonly string Username = username;
    [Key(1)]
    public readonly string Password = password;
    [Key(2)]
    public readonly ushort Version = version;
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
/// Sent from the server to the client,
/// if the client is accepted as a valid connection.<br/>
/// Contains the client's unique clientId.
/// </summary>
///
/// <remarks>
/// Server -&gt; Client
/// </remarks>
[MessagePackObject]
public readonly struct WelcomeMessage(uint clientId) : INetMessage
{
    [Key(0)]
    public readonly uint ClientId = clientId;
}*/
