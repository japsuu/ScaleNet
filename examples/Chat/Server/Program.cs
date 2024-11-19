using System.Net;
using ScaleNet;
using Server.Configuration;
using Shared;

namespace Server;

internal static class Program
{
    private static void Main(string[] args)
    {
        Logger logger = new();
        
        Console.Title = "COV Server";
        if (!ConfigManager.TryLoadConfiguration(logger))
        {
            logger.LogError("Failed to load configuration.");
            return;
        }
        
        // Create the server
        GameServer server = new(
            logger,
            IPAddress.Any,
            SharedConstants.SERVER_PORT,
            ConfigManager.CurrentConfiguration.MaxConnections,
            ConfigManager.CurrentConfiguration.AllowAccountRegistration);
        Console.WriteLine("startup");
        
        // Start the blocking server loop
        server.Run();
    }
}