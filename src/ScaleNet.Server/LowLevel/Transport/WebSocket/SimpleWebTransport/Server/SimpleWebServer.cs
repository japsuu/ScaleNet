using System.Net;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

public class SimpleWebServer
{
    private readonly int maxMessagesPerTick;

    public readonly WebSocketServer server;
    private readonly BufferPool bufferPool;


    public SimpleWebServer(int maxClients, int maxMessagesPerTick, TcpConfig tcpConfig, int maxMessageSize, int handshakeMaxSize, ServerSslContext? sslContext)
    {
        this.maxMessagesPerTick = maxMessagesPerTick;

        // use max because bufferpool is used for both messages and handshake
        int max = Math.Max(maxMessageSize, handshakeMaxSize);
        bufferPool = new BufferPool(5, 20, max);

        server = new WebSocketServer(maxClients, tcpConfig, maxMessageSize, handshakeMaxSize, sslContext, bufferPool);
    }


    public bool Active { get; private set; }

    public event Action<SessionId>? onConnect;
    public event Action<SessionId>? onDisconnect;
    public event Action<SessionId, ArraySegment<byte>>? onData;
    public event Action<SessionId, Exception>? onError;


    public void Start(ushort port)
    {
        server.Listen(port);
        Active = true;
    }


    public void Stop()
    {
        server.Stop();
        Active = false;
    }


    public void SendAll(HashSet<SessionId> connectionIds, byte[] data, int length)
    {
        ArrayBuffer buffer = bufferPool.Take(length);
        buffer.CopyFrom(data, 0, length);
        
        // Require buffer release from all connections before returning to pool
        buffer.SetReleasesRequired(connectionIds.Count);

        foreach (SessionId id in connectionIds)
            server.Send(id, buffer);
    }


    public void SendOne(SessionId connectionId, byte[] data, int length)
    {
        ArrayBuffer buffer = bufferPool.Take(length);
        buffer.CopyFrom(data, 0, length);

        server.Send(connectionId, buffer);
    }


    public bool KickClient(SessionId connectionId) => server.CloseConnection(connectionId);

    public EndPoint? GetClientAddress(SessionId connectionId) => server.GetClientEndPoint(connectionId);


    /// <summary>
    /// Processes all messages.
    /// </summary>
    public void ProcessMessageQueue()
    {
        int processedCount = 0;

        // check enabled every time incase behaviour was disabled after data
        while (
            processedCount < maxMessagesPerTick &&

            // Dequeue last
            server.ReceiveQueue.TryDequeue(out Message next)
        )
        {
            processedCount++;

            switch (next.type)
            {
                case EventType.Connected:
                    onConnect?.Invoke(next.connId);
                    break;
                case EventType.Data:
                    onData?.Invoke(next.connId, next.data.ToSegment());
                    next.data.Release();
                    break;
                case EventType.Disconnected:
                    onDisconnect?.Invoke(next.connId);
                    break;
                case EventType.Error:
                    onError?.Invoke(next.connId, next.exception);
                    break;
            }
        }
    }
}