using System.Text;
using Shared;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Client;

internal static class Program
{
    private const string ADDRESS = "127.0.0.1";
    private const int PORT = SharedConstants.SERVER_PORT;
    
    
    private static void Main(string[] args)
    {
        Console.Title = "COV Client";

        GameClient client = new(ADDRESS, PORT);
        client.Connect();

        Logger.LogInfo("Press Enter to stop the client or '!' to reconnect the client...");

        while (true)
        {
            string? line = Console.ReadLine();
            if (string.IsNullOrEmpty(line))
                break;

            if (line == "!")
            {
                client.Reconnect();
                continue;
            }

            client.SendMessageToServer(new ChatMessage(line));
        }

        client.Disconnect();
    }
}