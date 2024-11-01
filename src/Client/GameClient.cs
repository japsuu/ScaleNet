namespace Client;

internal class GameClient(string address, int port)
{
    private readonly TcpGameClient _tcpClient = new(address, port);


    public void Connect()
    {
        Console.Write($"Client connecting to {_tcpClient.Address}:{_tcpClient.Port}");
        
        if (_tcpClient.ConnectAsync())
            Console.WriteLine("Done!");
        else
        {
            Console.WriteLine("Connection failed!");
        }
    }


    public void Reconnect()
    {
        Console.Write("Client reconnecting...");
        _tcpClient.ReconnectAsync();
        Console.WriteLine("Done!");
    }


    public void Disconnect()
    {
        Console.Write("Client disconnecting...");
        _tcpClient.DisconnectAndStop();
        Console.WriteLine("Done!");
    }
}