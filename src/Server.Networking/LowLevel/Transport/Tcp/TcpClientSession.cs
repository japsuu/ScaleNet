using System.Collections.Concurrent;
using System.Net.Sockets;
using NetCoreServer;
using Shared.Networking;
using Shared.Utils;

namespace Server.Networking.LowLevel.Transport.Tcp;

internal class TcpClientSession(SessionId id, TcpServerTransport transport) : TcpSession(transport)
{
    public readonly SessionId SessionId = id;
    
    // Packets need to be stored per-session to, for example, allow sending all queued packets before disconnecting.
    public readonly ConcurrentQueue<TcpServerTransport.Packet> OutgoingPackets = new();
    public readonly ConcurrentQueue<TcpServerTransport.Packet> IncomingPackets = new();


    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        if (IncomingPackets.Count >= ServerConstants.MAX_PACKETS_PER_TICK)
        {
            Logger.LogWarning($"Session {SessionId} is sending too many packets. Kicking immediately.");
            transport.DisconnectSession(this, DisconnectReason.TooManyPackets);
            return;
        }
        
        ReadOnlyMemory<byte> memory = new(buffer, (int)offset, (int)size);
        
        transport.Middleware?.HandleIncomingPacket(ref memory);
        
        TcpServerTransport.Packet packet = new(memory);
        IncomingPackets.Enqueue(packet);
    }


    protected override void OnError(SocketError error)
    {
        Logger.LogError($"TCP session with Id {Id} caught an error: {error}");
    }
}