using System.Collections.Concurrent;
using System.Net.Sockets;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

internal sealed class Connection : IDisposable
{
    readonly object disposedLock = new object();

    public TcpClient client;

    public SessionId connId = SessionId.Invalid;
    public Stream stream;
    public Thread receiveThread;
    public Thread sendThread;

    public ManualResetEventSlim sendPending = new ManualResetEventSlim(false);
    public ConcurrentQueue<ArrayBuffer> sendQueue = new ConcurrentQueue<ArrayBuffer>();

    public Action<Connection> onDispose;

    volatile bool hasDisposed;

    public Connection(TcpClient client, Action<Connection> onDispose)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.onDispose = onDispose;
    }


    /// <summary>
    /// disposes client and stops threads
    /// </summary>
    public void Dispose()
    {
        SimpleWebLog.Verbose($"Dispose {ToString()}");

        // check hasDisposed first to stop ThreadInterruptedException on lock
        if (hasDisposed) { return; }

        SimpleWebLog.Info($"Connection Close: {ToString()}");


        lock (disposedLock)
        {
            // check hasDisposed again inside lock to make sure no other object has called this
            if (hasDisposed) { return; }
            hasDisposed = true;

            // stop threads first so they dont try to use disposed objects
            receiveThread.Interrupt();
            sendThread?.Interrupt();

            try
            {
                // stream 
                stream?.Dispose();
                stream = null;
                client.Dispose();
                client = null;
            }
            catch (Exception e)
            {
                SimpleWebLog.Exception(e);
            }

            sendPending.Dispose();

            // release all buffers in send queue
            while (sendQueue.TryDequeue(out ArrayBuffer buffer))
            {
                buffer.Release();
            }

            onDispose.Invoke(this);
        }
    }

    public override string ToString()
    {
        System.Net.EndPoint endpoint = client?.Client?.RemoteEndPoint;
        return $"[Conn:{connId}, endPoint:{endpoint}]";
    }
}