using System.Text;
using Server.Networking;
using Shared;

namespace Server.Packets;

internal class ChatMessageHandler : PacketHandler
{
    public override byte Id => 1;

    public override void Handle(PlayerSession playerSession, Packet packet)
    {
        string message = Encoding.UTF8.GetString(packet.Data.Array!, packet.Data.Offset, packet.Data.Count);
        Console.WriteLine($"Chat message received from player: {message}");

        // Broadcast message
        Console.WriteLine($"Broadcasting message to all players...");
        playerSession.Server.Multicast(packet.Data);
    }
}