﻿using System;
using MessagePack;
using ScaleNet.Networking;

namespace Shared
{
#region Chat

    [NetMessage(64)]
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

    [NetMessage(65)]
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