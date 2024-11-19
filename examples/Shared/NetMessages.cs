using MessagePack;
using ScaleNet;

namespace Shared
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
}