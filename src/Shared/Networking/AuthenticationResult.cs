namespace Shared.Networking;

public enum AuthenticationResult : byte
{
    Success,
    InvalidCredentials,
    AlreadyLoggedIn,
    AccountLocked,
}