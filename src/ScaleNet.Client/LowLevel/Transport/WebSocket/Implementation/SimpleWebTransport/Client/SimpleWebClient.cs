using System;
using System.Collections.Concurrent;
using ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Client.StandAlone;
using ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Client
{
    /// <summary>
    /// Client used to control websockets
    /// <para>Base class used by WebSocketClientWebGl and WebSocketClientStandAlone</para>
    /// </summary>
    public abstract class SimpleWebClient
    {
        public static SimpleWebClient Create(int maxMessageSize, int maxMessagesPerTick, TcpConfig tcpConfig, ClientSslContext? sslContext)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WebSocketClientWebGl(maxMessageSize, maxMessagesPerTick);
#else
            return new WebSocketClientStandAlone(maxMessageSize, maxMessagesPerTick, tcpConfig, sslContext);
#endif
        }


        private readonly int _maxMessagesPerTick;
        protected readonly int MaxMessageSize;
        protected readonly ConcurrentQueue<Message> ReceiveQueue = new();
        protected readonly BufferPool BufferPool;

        protected ConnectionState State;


        protected SimpleWebClient(int maxMessageSize, int maxMessagesPerTick)
        {
            MaxMessageSize = maxMessageSize;
            _maxMessagesPerTick = maxMessagesPerTick;
            BufferPool = new BufferPool(5, 20, maxMessageSize);
        }


        public ConnectionState ConnectionState => State;

        public event Action? OnConnect;
        public event Action? OnDisconnect;
        public event Action<ArraySegment<byte>>? OnData;
        public event Action<Exception>? OnError;


        /// <summary>
        /// Processes all messages.
        /// </summary>
        public void ProcessMessageQueue()
        {
            int processedCount = 0;
            while (processedCount < _maxMessagesPerTick && ReceiveQueue.TryDequeue(out Message next))
            {
                processedCount++;

                switch (next.Type)
                {
                    case EventType.Connected:
                        OnConnect?.Invoke();
                        break;
                    case EventType.Data:
                        OnData?.Invoke(next.Data!.ToSegment());
                        next.Data!.Release();
                        break;
                    case EventType.Disconnected:
                        OnDisconnect?.Invoke();
                        break;
                    case EventType.Error:
                        OnError?.Invoke(next.Exception!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }


        public abstract void Connect(Uri serverAddress);

        public abstract void Disconnect();

        public abstract void Send(byte[] data, int offset, int length);
    }
}