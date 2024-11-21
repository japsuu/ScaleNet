﻿namespace ScaleNet.Common
{
    public enum AccountCreationResult : byte
    {
        Success,
        UsernameTaken,
        InvalidUsername,
        InvalidPassword
    }
}