using System.Collections.Concurrent;
using System.Net.Sockets;
using NetCoreServer;
using Server.Packets;
using Shared;

namespace Server;

internal class PlayerSession(GameServer server, ConcurrentDictionary<byte, PacketHandler> packetHandlers) : TcpSession(server)
{
    private readonly GameServer _server = server;
    private readonly ConcurrentQueue<Packet> _incomingPackets = new();
    
    
    public void ProcessIncoming()
    {
        while (_incomingPackets.TryDequeue(out Packet packet))
        {
            if (!packetHandlers.TryGetValue(packet.Type, out PacketHandler? handler))
            {
                // Send error message and disconnect
                SendAsync("Invalid packet type!");
                Disconnect();
                continue;
            }

            handler.Handle(this, packet);
        }
    }
    
    
    protected override void OnConnected()
    {
        Console.WriteLine($"Chat TCP session with Id {Id} connected!");

        // Send invite message
        string message = "Hello from TCP chat! Please send a message or '!' to disconnect the client!";
        SendAsync(message);
    }


    protected override void OnDisconnected()
    {
        Console.WriteLine($"Chat TCP session with Id {Id} disconnected!");
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
        
        /*string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
        Console.WriteLine($"Incoming: {message}");

        // Multicast message to all connected sessions
        Server.Multicast(message);

        // If the buffer starts with '!' the disconnect the current session
        if (message == "!")
            Disconnect();*/
    }


    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"Chat TCP session caught an error with code {error}");
    }
}