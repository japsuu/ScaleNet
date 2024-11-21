namespace Shared.Authentication
{
    public enum AccountCreationResult : byte
    {
        Success,
        UsernameTaken,
        InvalidUsername,
        InvalidPassword
    }
}