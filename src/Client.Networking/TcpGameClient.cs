using System.Net.Sockets;
using System.Text;
using TcpClient = NetCoreServer.TcpClient;

namespace Client.Networking;

public class TcpGameClient(string address, int port) : TcpClient(address, port)
{
    private const int CONNECTION_RETRY_TIMEOUT_MS = 1000;
    
    private bool _stop;
    
    
    public void DisconnectAndStop()
    {
        _stop = true;
        DisconnectAsync();
        while (IsConnected)
            Thread.Yield();
    }


    protected override void OnConnected()
    {
        Console.WriteLine($"Client connected with session Id {Id}");
    }


    protected override void OnDisconnected()
    {
        Console.WriteLine($"Client disconnected with session Id {Id}");

        // Wait for a while...
        Thread.Sleep(CONNECTION_RETRY_TIMEOUT_MS);

        // Try to connect again
        if (!_stop)
            ConnectAsync();
    }


    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        Console.WriteLine(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));
    }


    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"Chat TCP client caught an error with code {error}");
    }
}