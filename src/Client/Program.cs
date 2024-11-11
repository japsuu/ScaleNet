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
        
        Logger.LogInfo("Enter the server address:");
        string? address = Console.ReadLine();
        if (string.IsNullOrEmpty(address))
            address = DEFAULT_ADDRESS;
        
        Logger.LogInfo("Enter the server port:");
        string? portStr = Console.ReadLine();
        if (string.IsNullOrEmpty(portStr) || !int.TryParse(portStr, out int port))
            port = DEFAULT_PORT;
        
        GameClient client = new(address, port);
        client.Run();

        client.Disconnect();
        Logger.LogError("Client disconnected. Press any key to exit.");
        Console.ReadKey();
    }
}