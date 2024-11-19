using MessagePack;
using ScaleNet.Utils;

namespace ScaleNet.Networking
{
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
    public interface INetMessage { }


    [MessagePackObject]
    public readonly struct DisconnectMessage : INetMessage
    {
        [Key(0)]
        public readonly DisconnectReason Reason;


        public DisconnectMessage(DisconnectReason reason)
        {
            Reason = reason;
        }
    }


    [MessagePackObject]
    public readonly struct AuthenticationInfoMessage : INetMessage
    {
        [Key(0)]
        public readonly bool RegistrationAllowed;
    
        [Key(1)]
        public readonly uint ServerVersion;


        public AuthenticationInfoMessage(bool registrationAllowed, uint serverVersion)
        {
            RegistrationAllowed = registrationAllowed;
            ServerVersion = serverVersion;
        }
    }


#region Account register

    [MessagePackObject]
    public readonly struct RegisterRequestMessage : INetMessage
    {
        [Key(0)]
        public readonly string Username;
    
        [Key(1)]
        public readonly string Password;


        public RegisterRequestMessage(string username, string password)
        {
            Username = username;
            Password = password;
        }
    }

    [MessagePackObject]
    public readonly struct RegisterResponseMessage : INetMessage
    {
        [Key(0)]
        public readonly AccountCreationResult Result;


        public RegisterResponseMessage(AccountCreationResult result)
        {
            Result = result;
        }
    }

#endregion


#region Account authentication

    [MessagePackObject]
    public readonly struct AuthenticationRequestMessage : INetMessage
    {
        [Key(0)]
        public readonly string Username;
    
        [Key(1)]
        public readonly string Password;


        public AuthenticationRequestMessage(string username, string password)
        {
            Username = username;
            Password = password;
        }
    }

    [MessagePackObject]
    public readonly struct AuthenticationResponseMessage : INetMessage
    {
        [Key(0)]
        public readonly AuthenticationResult Result;
    
        [Key(1)]
        public readonly uint ClientUid;


        public AuthenticationResponseMessage(AuthenticationResult result, uint clientUid)
        {
            Result = result;
            ClientUid = clientUid;
        }
    }

#endregion


#region Chat

    [MessagePackObject]
    public readonly struct ChatMessage : INetMessage
    {
        [Key(0)]
        public readonly string Message;


        public ChatMessage(string message)
        {
            Message = message;
        }
    }

    [MessagePackObject]
    public readonly struct ChatMessageNotification : INetMessage
    {
        [Key(0)]
        public readonly string User;
    
        [Key(1)]
        public readonly string Message;


        public ChatMessageNotification(string user, string message)
        {
            User = user;
            Message = message;
        }
    }

#endregion
}