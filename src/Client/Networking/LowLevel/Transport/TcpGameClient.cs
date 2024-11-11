using System.Net.Sockets;
using Shared.Networking;
using Shared.Utils;
using TcpClient = NetCoreServer.TcpClient;

namespace Client.Networking.LowLevel.Transport;

public class TcpGameClient(string address, int port) : TcpClient(address, port)
{
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    
    public event Action<ConnectionStateArgs>? ConnectionStateChanged;
    public event Action<Packet>? PacketReceived;


#region Public methods

    public void DisconnectAndStop()
    {
        Disconnect();
        while (IsConnected)
            Thread.Yield();
    }

#endregion


#region Lifetime

    protected override void OnConnecting()
    {
        ConnectionState prevState = _connectionState;
        _connectionState = ConnectionState.Connecting;
        ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
    }


    protected override void OnConnected()
    {
        ConnectionState prevState = _connectionState;
        _connectionState = ConnectionState.Connected;
        ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
    }

    
    protected override void OnDisconnecting()
    {
        ConnectionState prevState = _connectionState;
        _connectionState = ConnectionState.Disconnecting;
        ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
    }


    protected override void OnDisconnected()
    {
        ConnectionState prevState = _connectionState;
        _connectionState = ConnectionState.Disconnected;
        ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
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