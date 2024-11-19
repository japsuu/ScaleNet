using System;

namespace ScaleNet.Networking
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