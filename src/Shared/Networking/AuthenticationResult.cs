namespace ScaleNet.Networking;

public enum AuthenticationResult : byte
{
    Success,
    InvalidCredentials,
    AlreadyLoggedIn,
    AccountLocked,
}