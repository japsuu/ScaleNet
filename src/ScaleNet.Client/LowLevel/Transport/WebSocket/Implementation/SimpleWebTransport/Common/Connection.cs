using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common
{
    internal sealed class Connection : IDisposable
    {
        private readonly object _disposedLock = new();

        public TcpClient? Client;

        public Stream? Stream;
        public Thread? ReceiveThread;
        public Thread? SendThread;

        public readonly ManualResetEventSlim SendPending = new(false);
        public readonly ConcurrentQueue<ArrayBuffer> SendQueue = new();

        public readonly Action<Connection> OnDispose;

        private volatile bool _hasDisposed;


        public Connection(TcpClient client, Action<Connection> onDispose)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            OnDispose = onDispose;
        }


        /// <summary>
        /// disposes a client and stops its threads
        /// </summary>
        public void Dispose()
        {
            SimpleWebLog.Verbose($"Dispose {ToString()}");

            // check hasDisposed first to stop ThreadInterruptedException on lock
            if (_hasDisposed)
                return;

            SimpleWebLog.Info($"Connection Close: {ToString()}");


            lock (_disposedLock)
            {
                // check hasDisposed again inside lock to make sure no other object has called this
                if (_hasDisposed)
                    return;
                _hasDisposed = true;

                // stop threads first so they dont try to use disposed objects
                ReceiveThread?.Interrupt();
                SendThread?.Interrupt();

                try
                {
                    // stream 
                    Stream?.Dispose();
                    Stream = null;
                    Client?.Dispose();
                    Client = null;
                }
                catch (Exception e)
                {
                    SimpleWebLog.Exception(e);
                }

                SendPending.Dispose();

                // release all buffers in send queue
                while (SendQueue.TryDequeue(out ArrayBuffer? buffer))
                    buffer.Release();

                OnDispose.Invoke(this);
            }
        }


        public override string ToString()
        {
            System.Net.EndPoint endpoint = Client?.Client?.RemoteEndPoint!;
            return $"[EndPoint:{endpoint}]";
        }
    }
}