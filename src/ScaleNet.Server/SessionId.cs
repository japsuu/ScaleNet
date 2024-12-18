namespace ScaleNet.Server;

/// <summary>
/// ID of a client session/connection.
/// Changes when the client reconnects.
/// </summary>
public readonly struct SessionId(uint value) : IEquatable<SessionId>
{
    public const uint MAX_VALUE = uint.MaxValue;
    
    /// <summary>
    /// Special ID that represents an invalid session.
    /// </summary>
    public static readonly SessionId Invalid = new SessionId(0);
    
    /// <summary>
    /// Special ID that represents all available sessions.
    /// </summary>
    public static readonly SessionId Broadcast = new SessionId(MAX_VALUE);

    public uint Value { get; } = value;


    public override string ToString()
    {
        return Value.ToString();
    }

    public override bool Equals(object? obj)
    {
        return obj is SessionId sessionId && Value == sessionId.Value;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value);
    }

    public static bool operator ==(SessionId left, SessionId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SessionId left, SessionId right)
    {
        return !(left == right);
    }
    
    public static bool TryParse(string s, out SessionId sessionId)
    {
        if (uint.TryParse(s, out uint value))
        {
            sessionId = new SessionId(value);
            return true;
        }

        sessionId = Invalid;
        return false;
    }


    public bool Equals(SessionId other) => Value == other.Value;
}