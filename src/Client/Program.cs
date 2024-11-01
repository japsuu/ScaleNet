using System.Text;
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