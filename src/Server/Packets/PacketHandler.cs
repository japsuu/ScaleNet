using Server.Networking;
using Shared;
using Shared.Networking;

namespace Server.Packets;

internal abstract class PacketHandler
{
    public abstract byte Id { get; }
    public abstract void Handle(PlayerSession playerSession, Packet packet);
}