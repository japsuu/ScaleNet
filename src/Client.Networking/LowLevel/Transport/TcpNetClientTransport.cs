using System.Net.Sockets;
using Shared.Networking;
using Shared.Utils;
using TcpClient = NetCoreServer.TcpClient;

namespace Client.Networking.LowLevel.Transport;

public class TcpNetClientTransport(string address, int port) : TcpClient(address, port), INetClientTransport
{
    private IPacketMiddleware? _middleware;
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    
    public event Action<ConnectionStateArgs>? ConnectionStateChanged;
    public event Action<Packet>? PacketReceived;


    void INetClientTransport.Connect()
    {
        base.Connect();
    }


    void INetClientTransport.Reconnect()
    {
        base.Reconnect();
    }


    void INetClientTransport.Disconnect()
    {
        base.Disconnect();
    }


    void INetClientTransport.SendAsync(ReadOnlyMemory<byte> buffer)
    {
        _middleware?.HandleOutgoingPacket(ref buffer);
        
        bool success = base.SendAsync(buffer.Span);
        
        Logger.LogInfo($"TCP transport sent {buffer.Length} bytes: {success}");
    }


    void INetClientTransport.IterateIncomingPackets()
    {
        ReceiveAsync();
    }


    void INetClientTransport.SetPacketMiddleware(IPacketMiddleware? middleware)
    {
        _middleware = middleware;
    }


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


    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        ReadOnlyMemory<byte> p = new(buffer, (int)offset, (int)size);
        
        _middleware?.HandleIncomingPacket(ref p);
        
        Packet packet = new(p);
        
        PacketReceived?.Invoke(packet);
    }


    protected override void OnError(SocketError error)
    {
        Logger.LogError($"TCP transport caught an error with code {error}");
    }
}