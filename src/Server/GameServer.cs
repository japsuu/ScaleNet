using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using Server.Networking;
using Server.Packets;
using Shared;

namespace Server;

internal class GameServer
{
    private readonly TcpGameServer _tcpServer;
    private readonly ConcurrentDictionary<byte, PacketHandler> _packetHandlers = new();


    public GameServer(IPAddress address, int port)
    {
        _tcpServer = new TcpGameServer(address, port, OnReceivePacket);

        // Register packet handlers
        List<PacketHandler> handlers =
        [
            new ChatMessageHandler(),
            new DisconnectHandler()
        ];
        
        foreach (PacketHandler handler in handlers)
        {
            if (!_packetHandlers.TryAdd(handler.Id, handler))
                throw new InvalidOperationException($"Tried to add duplicate packet handler with Id {handler.Id} ({_packetHandlers[handler.Id].GetType()} and {handler.GetType()})");
        }
    }


    public void Start()
    {
        _tcpServer.Start();
    }


    public void Stop()
    {
        _tcpServer.Stop();
    }


    public void ProcessPackets()
    {
        _tcpServer.ProcessPackets();
    }


    private void OnReceivePacket(PlayerSession session, Packet packet)
    {
        if (!_packetHandlers.TryGetValue(packet.Type, out PacketHandler? handler))
        {
            // Send error message and disconnect
            session.SendAsync("Invalid packet type!");
            session.Disconnect();
            return;
        }

        handler.Handle(session, packet);
    }
}