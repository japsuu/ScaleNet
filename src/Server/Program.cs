using System.Net;
using Server.Networking;
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
        
        // Start the server
        GameServer server = new GameServer(IPAddress.Any, port);
        Console.WriteLine("startup");
        
        // Start the blocking server loop
        server.Run();

        Logger.LogInfo("Press any key to exit.");
        Console.ReadKey();
    }
}