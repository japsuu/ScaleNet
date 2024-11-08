using Shared;
using Shared.Networking.Messages;
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
        client.Connect();

        Logger.LogInfo("Press Enter to stop the client or '!' to reconnect the client...");

        while (true)
        {
            if (!client.IsAuthenticated)
                continue;
            
            string? line = Console.ReadLine();
            if (string.IsNullOrEmpty(line))
                break;

            if (line == "!")
            {
                client.Reconnect();
                continue;
            }
            
            ClearPreviousConsoleLine();

            client.SendMessageToServer(new ChatMessage(line));
        }

        client.Disconnect();
        Logger.LogError("Client disconnected. Press any key to exit.");
        Console.ReadKey();
    }
    
    private static void ClearPreviousConsoleLine()
    {
        int currentLineCursor = Console.CursorTop - 1;
        Console.SetCursorPosition(0, Console.CursorTop - 1);
        for (int i = 0; i < Console.WindowWidth; i++)
            Console.Write(" ");
        Console.SetCursorPosition(0, currentLineCursor);
    }
}