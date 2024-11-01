using Shared;

namespace Server.Packets;

internal abstract class PacketHandler
{
    public abstract byte Id { get; }
    public abstract void Handle(PlayerSession playerSession, Packet packet);
}