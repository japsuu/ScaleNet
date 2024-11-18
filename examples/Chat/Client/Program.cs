using ScaleNet;
using ScaleNet.Utils;

namespace Client;

internal static class Program
{
    private const string DEFAULT_ADDRESS = "127.0.0.1";
    private const int DEFAULT_PORT = SharedConstants.SERVER_PORT;
    
    
    private static void Main(string[] args)
    {
        Console.Title = "COV Client";

        (string address, int port) = GetAddressAndPort(args);

        GameClient client = new(address, port);
        
        // Start the blocking client loop
        client.Run();

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