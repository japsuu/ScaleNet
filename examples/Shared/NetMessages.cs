using MessagePack;
using ScaleNet.Common;
using Shared.Authentication;

namespace Shared
{
    public static class NetMessages
    {
#region Chat

        [NetMessage(0)]
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

        [NetMessage(1)]
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


    [NetMessage(2)]
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

    [NetMessage(3)]
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

    [NetMessage(4)]
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

    [NetMessage(5)]
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

    [NetMessage(6)]
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
    }
}