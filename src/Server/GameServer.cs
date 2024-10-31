using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using Server.Packets;

namespace Server;

internal class GameServer : TcpServer
{
    private readonly ConcurrentDictionary<byte, PacketHandler> _packetHandlers = new();


    public GameServer(IPAddress address, int port) : base(address, port)
    {
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


    public void ProcessPackets()
    {
        //TODO: Parallelize
        foreach (TcpSession tcpSession in Sessions.Values)
        {
            PlayerSession? session = (PlayerSession)tcpSession;
            session.ProcessIncoming();
        }
    }
    
    
    protected override TcpSession CreateSession() => new PlayerSession(this, _packetHandlers);


    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"Chat TCP server caught an error with code {error}");
    }
}