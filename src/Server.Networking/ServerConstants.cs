namespace Server.Networking;

/// <summary>
/// Constants only used on the server.
/// </summary>
public static class ServerConstants
{
    private const int MAX_PACKETS_PER_SECOND = 10;
    
    public const int TICKS_PER_SECOND = 5;
    public const int MAX_PACKETS_PER_TICK = MAX_PACKETS_PER_SECOND / TICKS_PER_SECOND;
    public const int MAX_CONNECTIONS = 1000;
}