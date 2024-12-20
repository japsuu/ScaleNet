using System.Net;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

public class SimpleWebServer
{
    private readonly int _maxMessagesPerTick;
    private readonly WebSocketServer _wsServer;
    private readonly BufferPool _bufferPool;
    
    public bool Active { get; private set; }

    public event Action<ConnectionId>? OnConnect;
    public event Action<ConnectionId>? OnDisconnect;
    public event Action<ConnectionId, ArraySegment<byte>>? OnData;
    public event Action<ConnectionId, Exception>? OnError;


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


    public void SendAll(HashSet<ConnectionId> connectionIds, byte[] data, int offset, int length)
    {
        ArrayBuffer buffer = _bufferPool.Take(length);
        buffer.CopyFrom(data, offset, length);

        // Require buffer release from all connections before returning to pool
        buffer.SetReleasesRequired(connectionIds.Count);

        foreach (ConnectionId id in connectionIds)
            _wsServer.Send(id, buffer);
    }


    public void SendOne(ConnectionId connectionId, byte[] data, int offset, int length)
    {
        ArrayBuffer buffer = _bufferPool.Take(length);
        buffer.CopyFrom(data, offset, length);

        _wsServer.Send(connectionId, buffer);
    }


    public bool KickClient(ConnectionId connectionId) => _wsServer.CloseConnection(connectionId);

    public EndPoint? GetClientAddress(ConnectionId connectionId) => _wsServer.GetClientEndPoint(connectionId);


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