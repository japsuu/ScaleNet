using System.Text;
using Shared;

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

            // Create a packet
            byte[] payload = Encoding.UTF8.GetBytes(line);
            byte[] packet = new byte[payload.Length + 2];
            
            // Version
            packet[0] = 1;
            
            // Type
            packet[1] = 1;
            
            // Payload
            Array.Copy(payload, 0, packet, 2, payload.Length);
            
            // Send the packet
            client.SendPacket(packet);
        }

        client.Disconnect();
    }
}