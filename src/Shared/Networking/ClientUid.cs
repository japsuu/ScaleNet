namespace Shared.Networking;

/// <summary>
/// A unique identifier for a specific client, assigned on account creation.
/// </summary>
public readonly struct ClientUid(uint value) : IEquatable<ClientUid>
{
    public static readonly ClientUid Invalid = new ClientUid(0);

    public uint Value { get; } = value;


    public override string ToString()
    {
        return Value.ToString();
    }

    public override bool Equals(object? obj)
    {
        return obj is ClientUid sessionId && Value == sessionId.Value;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value);
    }

    public static bool operator ==(ClientUid left, ClientUid right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ClientUid left, ClientUid right)
    {
        return !(left == right);
    }


    public bool Equals(ClientUid other) => Value == other.Value;
}