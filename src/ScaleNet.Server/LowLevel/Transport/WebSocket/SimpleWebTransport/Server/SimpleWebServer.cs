using System.Net;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

public class SimpleWebServer
{
    private readonly int _maxMessagesPerTick;
    private readonly WebSocketServer _wsServer;
    private readonly BufferPool _bufferPool;
    
    public bool Active { get; private set; }

    public event Action<SessionId>? OnConnect;
    public event Action<SessionId>? OnDisconnect;
    public event Action<SessionId, ArraySegment<byte>>? OnData;
    public event Action<SessionId, Exception>? OnError;


    public SimpleWebServer(int maxClients, int maxMessagesPerTick, TcpConfig tcpConfig, int maxPacketSize, int handshakeMaxSize, ServerSslContext? sslContext)
    {
        _maxMessagesPerTick = maxMessagesPerTick;

        // Use max because the buffer pool is used for both messages and handshake
        int max = Math.Max(maxPacketSize, handshakeMaxSize);
        _bufferPool = new BufferPool(5, 20, max);

        _wsServer = new WebSocketServer(maxClients, tcpConfig, maxPacketSize, handshakeMaxSize, sslContext, _bufferPool);
    }


    public void Start(ushort port)
    {
        _wsServer.Listen(port);
        Active = true;
    }


    public void Stop()
    {
        _wsServer.Stop();
        Active = false;
    }


    public void SendAll(HashSet<SessionId> connectionIds, byte[] data, int length)
    {
        ArrayBuffer buffer = _bufferPool.Take(length);
        buffer.CopyFrom(data, 0, length);

        // Require buffer release from all connections before returning to pool
        buffer.SetReleasesRequired(connectionIds.Count);

        foreach (SessionId id in connectionIds)
            _wsServer.Send(id, buffer);
    }


    public void SendOne(SessionId connectionId, byte[] data, int length)
    {
        ArrayBuffer buffer = _bufferPool.Take(length);
        buffer.CopyFrom(data, 0, length);

        _wsServer.Send(connectionId, buffer);
    }


    public bool KickClient(SessionId connectionId) => _wsServer.CloseConnection(connectionId);

    public EndPoint? GetClientAddress(SessionId connectionId) => _wsServer.GetClientEndPoint(connectionId);


    /// <summary>
    /// Processes all messages.
    /// </summary>
    public void ProcessMessageQueue()
    {
        int processedCount = 0;

        while (processedCount < _maxMessagesPerTick && _wsServer.ReceiveQueue.TryDequeue(out Message next))
        {
            processedCount++;

            switch (next.Type)
            {
                case EventType.Connected:
                    OnConnect?.Invoke(next.ConnId);
                    break;
                case EventType.Data:
                    OnData?.Invoke(next.ConnId, next.Data!.ToSegment());
                    next.Data!.Release();
                    break;
                case EventType.Disconnected:
                    OnDisconnect?.Invoke(next.ConnId);
                    break;
                case EventType.Error:
                    OnError?.Invoke(next.ConnId, next.Exception!);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}