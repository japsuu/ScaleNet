using System.Net;
using Server.Configuration;
using Shared;
using Shared.Utils;

namespace Server;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.Title = "COV Server";
        if (!ConfigManager.TryLoadConfiguration())
        {
            Logger.LogError("Failed to load configuration.");
            return;
        }
        
        // Create the server
        GameServer server = new(
            IPAddress.Any,
            SharedConstants.SERVER_PORT,
            ConfigManager.CurrentConfiguration.MaxConnections,
            ConfigManager.CurrentConfiguration.AllowAccountRegistration);
        Console.WriteLine("startup");
        
        // Start the blocking server loop
        server.Run();
    }
}