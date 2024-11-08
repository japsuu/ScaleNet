using System.Net.Sockets;
using NetCoreServer;
using Shared.Networking;
using Shared.Utils;

namespace Server.Networking.LowLevel;

/// <summary>
/// A network session for a player.
/// Queues incoming packets for processing.
/// </summary>
/// <param name="server">The server that the session is connected to.</param>
internal class ClientConnection(TcpGameServer server) : TcpSession(server)
{
    public bool RejectNewPackets { get; set; }
    
    public event Action<Packet>? PacketReceived;


    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        if (RejectNewPackets)
            return;
        
        if (PacketReceived == null)
        {
            Logger.LogWarning("No packet received event handler is set!");
            return;
        }
        
        Packet packet = new(buffer, (int)offset, (int)size);
        PacketReceived.Invoke(packet);
    }


    protected override void OnError(SocketError error)
    {
        Logger.LogError($"TCP session of player with Id {Id} caught an error with code {error}");
    }
}