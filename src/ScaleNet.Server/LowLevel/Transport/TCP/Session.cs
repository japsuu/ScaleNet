using System.Collections.Concurrent;
using ScaleNet.Common;

namespace ScaleNet.Server.LowLevel.Transport.Tcp;

internal sealed class Session : IDisposable
{
    // Packets need to be stored per-session to, for example, allow sending all queued packets before disconnecting.
    public readonly ConcurrentQueue<DeserializedNetMessage> IncomingMessages = new();
    public readonly ConcurrentQueue<SerializedNetMessage> OutgoingPackets = new();


    public void Dispose()
    {
        IncomingMessages.Clear();
        
        // Empty the queue by dequeuing all elements and returning the buffers to the pool.
        while (OutgoingPackets.TryDequeue(out SerializedNetMessage msg))
            msg.Dispose();
    }
}