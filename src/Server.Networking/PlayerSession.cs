using System.Collections.Concurrent;
using System.Net.Sockets;
using NetCoreServer;
using Shared;

namespace Server.Networking;

public class PlayerSession(TcpGameServer server, Action<PlayerSession, Packet> onReceivePacket) : TcpSession(server)
{
    private readonly ConcurrentQueue<Packet> _incomingPackets = new();
    
    
    public void ProcessIncoming()
    {
        while (_incomingPackets.TryDequeue(out Packet packet))
        {
            onReceivePacket(this, packet);
        }
    }
    
    
    protected override void OnConnected()
    {
        Console.WriteLine($"TCP session with Id {Id} connected!");

        // Send invite message
        string message = "Hello from TCP! Please send a message or '!' to disconnect the client!";
        SendAsync(message);
    }


    protected override void OnDisconnected()
    {
        Console.WriteLine($"TCP session with Id {Id} disconnected!");
    }


    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        // Verify packet version
        if (buffer[offset] != SharedConstants.PACKET_FORMAT_VERSION)
        {
            // Send error message and disconnect
            SendAsync("Invalid packet version!");
            Disconnect();
            return;
        }
        
        // Parse packet
        Packet packet = new Packet(buffer, (int)offset, (int)size);
        
        // Enqueue incoming packet
        _incomingPackets.Enqueue(packet);
    }


    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"TCP session caught an error with code {error}");
    }
}