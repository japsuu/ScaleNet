using System.Net.Sockets;
using Shared.Networking;
using Shared.Utils;
using TcpClient = NetCoreServer.TcpClient;

namespace Client.Networking;

public class TcpGameClient(string address, int port) : TcpClient(address, port)
{
    private const int CONNECTION_RETRY_TIMEOUT_MS = 1000;
    
    private bool _stop;
    
    public event Action<ConnectionStateArgs>? ConnectionStateChanged;
    public event Action<Packet>? PacketReceived;


#region Public methods

    public void DisconnectAndStop()
    {
        _stop = true;
        DisconnectAsync();
        while (IsConnected)
            Thread.Yield();
    }

#endregion


#region Connect

    protected override void OnConnecting()
    {
        ConnectionStateChanged?.Invoke(new ConnectionStateArgs(ConnectionState.Connecting));
    }


    protected override void OnConnected()
    {
        ConnectionStateChanged?.Invoke(new ConnectionStateArgs(ConnectionState.Connected));
    }

#endregion


#region Disconnect

    protected override void OnDisconnecting()
    {
        ConnectionStateChanged?.Invoke(new ConnectionStateArgs(ConnectionState.Disconnecting));
    }


    protected override void OnDisconnected()
    {
        ConnectionStateChanged?.Invoke(new ConnectionStateArgs(ConnectionState.Disconnected));

        // Wait for a while...
        Thread.Sleep(CONNECTION_RETRY_TIMEOUT_MS);

        // Try to connect again
        if (!_stop)
            ConnectAsync();
    }

#endregion


#region Data receive

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        Packet packet = new(buffer, (int)offset, (int)size);
        
        PacketReceived?.Invoke(packet);
    }

#endregion


#region Error handling

    protected override void OnError(SocketError error)
    {
        Logger.LogError($"Chat TCP client caught an error with code {error}");
    }

#endregion
}