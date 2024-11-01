using Client.Networking;

namespace Client;

internal class GameClient(string address, int port)
{
    private readonly TcpGameClient _tcpClient = new(address, port);


    public void Connect()
    {
        Console.WriteLine($"Client connecting to {_tcpClient.Address}:{_tcpClient.Port}");
        
        if (_tcpClient.ConnectAsync())
            Console.WriteLine("Done!");
        else
        {
            Console.WriteLine("Connection failed!");
        }
    }


    public void Reconnect()
    {
        Console.WriteLine("Client reconnecting...");
        _tcpClient.ReconnectAsync();
        Console.WriteLine("Done!");
    }


    public void Disconnect()
    {
        Console.WriteLine("Client disconnecting...");
        _tcpClient.DisconnectAndStop();
        Console.WriteLine("Done!");
    }
    
    
    public void SendPacket(byte[] packet)
    {
        _tcpClient.SendAsync(packet);

        foreach (byte b in packet)
        {
            // Write the packet bytes as binary, padded with zeros
            Console.WriteLine(Convert.ToString(b, 2).PadLeft(8, '0'));
        }
    }
}