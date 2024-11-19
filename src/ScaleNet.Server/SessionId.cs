using System;

namespace ScaleNet.Server;

/// <summary>
/// ID of a client session/connection.
/// Changes when the client reconnects.
/// </summary>
public readonly struct SessionId(uint value) : IEquatable<SessionId>
{
    public static readonly SessionId Invalid = new SessionId(0);

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


    public bool Equals(SessionId other) => Value == other.Value;
}