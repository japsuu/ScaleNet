using System.Net;
using Shared;
using Shared.Utils;

namespace Server;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.Title = "COV Server";
        
        // TCP server port
        int port = SharedConstants.SERVER_PORT;
        if (args.Length > 0)
            port = int.Parse(args[0]);

        Logger.LogInfo($"Using TCP port {port}\n");
        
        // Start the server
        GameServer server = new GameServer(IPAddress.Any, port);
        server.Start();
        Console.WriteLine("startup");

        while (true)
        {
            server.ProcessPackets();
            
            Thread.Sleep(1000 / ServerConstants.TICKS_PER_SECOND);
            
            /*string? line = Console.ReadLine();
            if (string.IsNullOrEmpty(line))
                break;

            // Restart the server
            if (line == "!")
            {
                Console.Write("Server restarting...");
                server.Restart();
                Console.WriteLine("Done!");
                continue;
            }

            // Multicast admin message to all sessions
            line = $"(admin) {line}";
            server.Multicast(line);*/
        }

        // Stop the server
        Logger.LogInfo("Server stopping...");
        server.Stop();
        Logger.LogInfo("Done!");
    }
}