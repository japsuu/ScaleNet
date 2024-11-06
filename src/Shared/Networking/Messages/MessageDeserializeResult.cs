namespace Shared.Networking.Messages;

public enum MessageDeserializeResult : byte
{
    Success,
    MalformedData,
    OutdatedVersion
}