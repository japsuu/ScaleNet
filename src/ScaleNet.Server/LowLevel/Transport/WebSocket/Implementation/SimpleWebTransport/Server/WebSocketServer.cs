using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

public class WebSocketServer
{
    public readonly ConcurrentQueue<Message> ReceiveQueue = new();

    private readonly TcpConfig _tcpConfig;
    private readonly int _maxPacketSize;

    private TcpListener? _listener;
    private Thread? _acceptThread;
    private bool _isServerStopped;
    private readonly ServerHandshakeHandler _handshakeHandler;
    private readonly ServerSslHelper _sslHelper;
    private readonly BufferPool _bufferPool;
    private readonly ConcurrentDictionary<ConnectionId, Common.Connection> _connections = new();

    private readonly ConcurrentBag<uint> _availableConnectionIds = [];


    public WebSocketServer(int maxClients, TcpConfig tcpConfig, int maxPacketSize, int handshakeMaxSize, ServerSslContext? sslContext, BufferPool bufferPool)
    {
        _tcpConfig = tcpConfig;
        _maxPacketSize = maxPacketSize;
        _sslHelper = new ServerSslHelper(sslContext);
        _bufferPool = bufferPool;
        _handshakeHandler = new ServerHandshakeHandler(_bufferPool, handshakeMaxSize);

        // Fill the available connectionId IDs bag.
        for (uint i = 1; i < maxClients; i++)
        {
            if (!ConnectionId.IsReserved(i))
                _availableConnectionIds.Add(i);
        }
    }


    public void Listen(int port)
    {
        _listener = TcpListener.Create(port);
        _listener.Start();
        SimpleWebLog.Info($"Server has started on port {port}");

        _acceptThread = new Thread(AcceptLoop)
        {
            IsBackground = true
        };
        _acceptThread.Start();
    }


    public void Stop()
    {
        _isServerStopped = true;

        // Interrupt then stop so that Exception is handled correctly
        _acceptThread?.Interrupt();
        _listener?.Stop();
        _acceptThread = null;
        
        // make copy so that foreach doesn't break if values are removed
        Common.Connection[] connectionsCopy = _connections.Values.ToArray();
        foreach (Common.Connection conn in connectionsCopy)
            conn.Dispose();

        _connections.Clear();
    }


    private void AcceptLoop()
    {
        try
        {
            try
            {
                while (true)
                {
                    TcpClient client = _listener!.AcceptTcpClient();
                    TcpConfig.Apply(_tcpConfig, client);
                    
                    Common.Connection conn = new(client, AfterConnectionDisposed);

                    // handshake needs its own thread as it needs to wait for a message from the client
                    Thread receiveThread = new(() => HandshakeAndReceiveLoop(conn));

                    conn.ReceiveThread = receiveThread;

                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
            }
            catch (SocketException)
            {
                // check for Interrupted/Abort
                Utils.SleepForInterrupt();
                throw;
            }
        }
        catch (ThreadInterruptedException e)
        {
            SimpleWebLog.InfoException(e);
        }
        catch (ThreadAbortException e)
        {
            SimpleWebLog.InfoException(e);
        }
        catch (Exception e)
        {
            SimpleWebLog.Exception(e);
        }
    }


    private void HandshakeAndReceiveLoop(Common.Connection conn)
    {
        try
        {
            bool success = _sslHelper.TryCreateStream(conn);
            if (!success)
            {
                conn.Dispose();
                return;
            }

            success = _handshakeHandler.TryHandshake(conn);

            if (!success)
            {
                conn.Dispose();
                return;
            }

            // check if Stop has been called since accepting this client
            if (_isServerStopped)
                return;

            bool isIdAvailable = _availableConnectionIds.TryTake(out uint uId);

            if (!isIdAvailable)
            {
                SimpleWebLog.Warn("Ran out of available connectionId IDs. A client attempting to connect will be rejected.");
                conn.Dispose();
                return;
            }

            conn.ConnId = new ConnectionId(uId);

            _connections.TryAdd(conn.ConnId, conn);

            ReceiveQueue.Enqueue(new Message(conn.ConnId, EventType.Connected));

            Thread sendThread = new(
                () =>
                {
                    SendLoop.Config sendConfig = new(
                        conn,
                        Constants.HEADER_SIZE + _maxPacketSize,
                        false);

                    SendLoop.Loop(sendConfig);
                });

            conn.SendThread = sendThread;
            sendThread.IsBackground = true;
            sendThread.Name = $"SendLoop {conn.ConnId}";
            sendThread.Start();

            ReceiveLoop.Config receiveConfig = new(
                conn,
                _maxPacketSize,
                true,
                ReceiveQueue,
                _bufferPool);

            ReceiveLoop.Loop(receiveConfig);
        }
        catch (ThreadInterruptedException e)
        {
            SimpleWebLog.InfoException(e);
        }
        catch (ThreadAbortException e)
        {
            SimpleWebLog.InfoException(e);
        }
        catch (Exception e)
        {
            SimpleWebLog.Exception(e);
        }
        finally
        {
            // close here in case connect fails
            conn.Dispose();
        }
    }


    private void AfterConnectionDisposed(Common.Connection conn)
    {
        if (conn.ConnId == ConnectionId.Invalid)
            return;
        
        ReceiveQueue.Enqueue(new Message(conn.ConnId, EventType.Disconnected));
        _connections.TryRemove(conn.ConnId, out Common.Connection? _);
        _availableConnectionIds.Add(conn.ConnId.Value);
    }


    public void Send(ConnectionId id, ArrayBuffer buffer)
    {
        if (_connections.TryGetValue(id, out Common.Connection? conn))
        {
            conn.SendQueue.Enqueue(buffer);
            conn.SendPending.Set();
        }
        else
            SimpleWebLog.Warn($"Cant send message to {id} because connection was not found in dictionary. Maybe it disconnected.");
    }


    public bool CloseConnection(ConnectionId id)
    {
        if (_connections.TryGetValue(id, out Common.Connection? conn))
        {
            SimpleWebLog.Info($"Kicking connection {id}");
            conn.Dispose();
            return true;
        }

        SimpleWebLog.Warn($"Failed to kick {id} because id not found");

        return false;
    }


    public EndPoint? GetClientEndPoint(ConnectionId id)
    {
        if (_connections.TryGetValue(id, out Common.Connection? conn))
            return conn.Client!.Client.RemoteEndPoint;

        SimpleWebLog.Error($"Cant get endpoint of connection to {id} because connection was not found in dictionary");
        return null;
    }
}