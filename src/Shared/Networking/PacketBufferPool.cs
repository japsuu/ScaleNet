using NetStack.Serialization;

namespace Shared.Networking;

/// <summary>
/// A thread-local buffer pool for serializing and deserializing network packets.
/// </summary>
public static class PacketBufferPool
{
    [ThreadStatic]
    private static BitBuffer? bitBuffer;


    public static BitBuffer GetBitBuffer()
    {
        return bitBuffer ??= new BitBuffer(SharedConstants.MAX_PACKET_SIZE_BYTES / 4);
    }
}