namespace ScaleNet
{
    public enum AccountCreationResult : byte
    {
        Success,
        UsernameTaken,
        InvalidUsername,
        InvalidPassword
    }
}