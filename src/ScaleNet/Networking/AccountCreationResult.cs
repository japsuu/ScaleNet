namespace ScaleNet.Networking
{
    public enum AccountCreationResult : byte
    {
        Success,
        UsernameTaken,
        InvalidUsername,
        InvalidPassword
    }
}