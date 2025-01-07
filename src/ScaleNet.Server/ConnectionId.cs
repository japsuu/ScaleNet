namespace ScaleNet.Server;

/// <summary>
/// ID of a client session/connection.
/// Changes when the client reconnects.
/// </summary>
public readonly struct ConnectionId(uint value) : IEquatable<ConnectionId>
{
    public const uint MAX_VALUE = uint.MaxValue;
    public const uint INVALID_VALUE = 0;
    
    /// <summary>
    /// Special ID that represents an invalid connectionId.
    /// </summary>
    public static readonly ConnectionId Invalid = new ConnectionId(INVALID_VALUE);
    
    /// <summary>
    /// Special ID that represents all available sessions.
    /// </summary>
    public static readonly ConnectionId Broadcast = new ConnectionId(MAX_VALUE);

    public uint Value { get; } = value;
    
    
    public static bool IsReserved(uint value) => value == MAX_VALUE || value == INVALID_VALUE;


    public override string ToString()
    {
        return Value.ToString();
    }

    public override bool Equals(object? obj)
    {
        return obj is ConnectionId connId && Value == connId.Value;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value);
    }

    public static bool operator ==(ConnectionId left, ConnectionId right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ConnectionId left, ConnectionId right)
    {
        return !(left == right);
    }
    
    public static bool TryParse(string s, out ConnectionId connectionId)
    {
        if (uint.TryParse(s, out uint value))
        {
            connectionId = new ConnectionId(value);
            return true;
        }

        connectionId = Invalid;
        return false;
    }


    public bool Equals(ConnectionId other) => Value == other.Value;
}