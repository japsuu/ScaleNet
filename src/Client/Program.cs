using Shared;
using Shared.Utils;

namespace Client;

internal static class Program
{
    private const string DEFAULT_ADDRESS = "127.0.0.1";
    private const int DEFAULT_PORT = SharedConstants.SERVER_PORT;
    
    
    private static void Main(string[] args)
    {
        Console.Title = "COV Client";

        (string address, int port) = GetAddressAndPort(args);
        
        const int clientCount = 999;
        List<GameClient> clients = [];
        
        Thread.Sleep(3000);
        
        // Create
        for (int i = 0; i < clientCount; i++)
        {
            GameClient client = new(address, port);
            clients.Add(client);
        }
        
        // Connect
        foreach (GameClient client in clients)
            client.Connect();
        
        // Wait for all clients to connect and authenticate
        for (int i = 0; i < clients.Count; i++)
        {
            GameClient c = clients[i];
            while (!c.IsConnected || !c.IsAuthenticated)
                Thread.SpinWait(0);
            Console.WriteLine($"Client {i} connected and authenticated.");
        }

        // Send test messages
        for (int i = 0; i < clientCount; i++)
            clients[i].SendTestMessage(i);
        
        // Wait for 5 seconds
        Thread.Sleep(5000);
        
        // Disconnect
        foreach (GameClient client in clients)
            client.Disconnect();

        Logger.LogInfo("Press any key to exit.");
        Console.ReadKey();
    }


    private static (string, int) GetAddressAndPort(string[] args)
    {
        string? address = null;
        string? portStr = null;
        
        // Try to read address & port from args in address:port format
        if (args.Length > 0)
        {
            string[] parts = args[0].Split(':');
            if (parts.Length == 2)
            {
                address = parts[0];
                portStr = parts[1];
            }
        }
        
        if (string.IsNullOrEmpty(address))
        {
            Logger.LogInfo("Enter the server address:");
            address = Console.ReadLine();
        }
        if (string.IsNullOrEmpty(address))
            address = DEFAULT_ADDRESS;

        if (string.IsNullOrEmpty(portStr))
        {
            Logger.LogInfo("Enter the server port:");
            portStr = Console.ReadLine();
        }
        if (string.IsNullOrEmpty(portStr) || !int.TryParse(portStr, out int port))
            port = DEFAULT_PORT;
        return (address, port);
    }
}