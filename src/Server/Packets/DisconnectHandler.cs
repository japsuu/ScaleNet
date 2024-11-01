using Server.Networking;
using Shared;

namespace Server.Packets;

internal class DisconnectHandler : PacketHandler
{
    public override byte Id => 2;

    public override void Handle(PlayerSession playerSession, Packet packet)
    {
        Console.WriteLine("Disconnect packet received!");

        // Disconnect the player
        playerSession.Disconnect();
    }
}