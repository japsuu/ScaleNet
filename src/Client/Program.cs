using Shared;

namespace Client;

internal static class Program
{
    private static void Main(string[] args)
    {
        const string address = "127.0.0.1";
        const int port = SharedConstants.SERVER_PORT;

        GameClient client = new(address, port);
        client.Connect();

        Console.WriteLine("Press Enter to stop the client or '!' to reconnect the client...");

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

            // Send the entered text to the chat server
            client.SendAsync(line);
        }

        client.Disconnect();
    }
}