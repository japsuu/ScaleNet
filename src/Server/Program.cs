using System.Net;
using Shared;

namespace Server;

internal static class Program
{
    private static void Main(string[] args)
    {
        // TCP server port
        int port = SharedConstants.SERVER_PORT;
        if (args.Length > 0)
            port = int.Parse(args[0]);

        Console.WriteLine($"Using TCP port {port}\n");
        
        // Start the server
        Console.Write("Server starting...");
        GameServer server = new GameServer(IPAddress.Any, port);
        server.Start();
        Console.WriteLine("Done!");

        while (true)
        {
            server.ProcessPackets();
            
            Thread.Sleep(200);
            
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
        Console.Write("Server stopping...");
        server.Stop();
        Console.WriteLine("Done!");
    }
}