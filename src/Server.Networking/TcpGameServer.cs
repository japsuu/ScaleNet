using System.Net;
using System.Net.Sockets;
using NetCoreServer;
using Shared;

namespace Server.Networking;

public class TcpGameServer : TcpServer
{
    private readonly Action<PlayerSession, Packet> _onReceivePacket;
    
    public TcpGameServer(IPAddress address, int port, Action<PlayerSession, Packet> onReceivePacket) : base(address, port)
    {
        _onReceivePacket = onReceivePacket;
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
    
    
    protected override TcpSession CreateSession() => new PlayerSession(this, _onReceivePacket);


    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"Chat TCP server caught an error with code {error}");
    }
}