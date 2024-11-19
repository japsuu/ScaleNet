using System;

namespace ScaleNet
{
    public enum AuthenticationResult : byte
    {
        Success,
        InvalidCredentials,
        [Obsolete("Not implemented yet")]
        AlreadyLoggedIn,
        [Obsolete("Not implemented yet")]
        AccountLocked,
    }
}