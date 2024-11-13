using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Shared.Networking;

namespace Server.Networking.Database;

internal static class InMemoryMockDatabase
{
    private static readonly ConcurrentDictionary<ClientUid, string> Usernames = new();
    
    
    public static void AddUsername(ClientUid clientUid, string username)
    {
        Usernames.TryAdd(clientUid, username);
    }
    
    
    public static bool TryGetUsername(ClientUid clientUid, [NotNullWhen(true)]out string? username)
    {
        return Usernames.TryGetValue(clientUid, out username);
    }
}