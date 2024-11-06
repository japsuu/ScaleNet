using NetStack.Serialization;

namespace Shared.Networking.Messages;

/// <summary>
/// Sent by the client in response to a <see cref="AuthRequestMessage"/>.
/// </summary>
///
/// <remarks>
/// Client -&gt; Server
/// </remarks>
public class AuthResponseMessage : NetMessage
{
    public string Username { get; private set; }
    public string Password { get; private set; }


    /// <summary>
    /// Constructs a new AuthPasswordNetMessage.
    /// </summary>
    /// <param name="username">Username to authenticate with. Has a 24-character limit.</param>
    /// <param name="password">Password to authenticate with. Has a 24-character limit.</param>
    public AuthResponseMessage(string username, string password)
    {
        Username = username;
        Password = password;
    }


    protected override void SerializeInternal(BitBuffer buffer)
    {
        buffer.AddString(Username);
        buffer.AddString(Password);
    }


    protected override MessageDeserializeResult DeserializeInternal(BitBuffer buffer)
    {
        Username = buffer.ReadString();
        Password = buffer.ReadString();
        
        return MessageDeserializeResult.Success;
    }
}