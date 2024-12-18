using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

public class WebSocketServer
{
    public readonly ConcurrentQueue<Message> ReceiveQueue = new();

    private readonly TcpConfig _tcpConfig;
    private readonly int _maxMessageSize;

    public TcpListener Listener;
    private Thread _acceptThread;
    private bool _serverStopped;
    private readonly int _maxClients;
    private readonly ServerHandshake _handShake;
    private readonly ServerSslHelper _sslHelper;
    private readonly BufferPool _bufferPool;
    private readonly ConcurrentDictionary<SessionId, Common.Connection> _connections = new();

    private readonly ConcurrentBag<uint> _availableSessionIds = [];


    public WebSocketServer(int maxClients, TcpConfig tcpConfig, int maxMessageSize, int handshakeMaxSize, ServerSslContext? sslContext, BufferPool bufferPool)
    {
        _maxClients = maxClients;
        this._tcpConfig = tcpConfig;
        this._maxMessageSize = maxMessageSize;
        _sslHelper = new ServerSslHelper(sslContext);
        this._bufferPool = bufferPool;
        _handShake = new ServerHandshake(this._bufferPool, handshakeMaxSize);

        // Fill the available session IDs bag.
        for (uint i = 1; i < maxClients; i++)
            _availableSessionIds.Add(i);
    }


    public void Listen(int port)
    {
        Listener = TcpListener.Create(port);
        Listener.Start();
        SimpleWebLog.Info($"Server has started on port {port}");

        _acceptThread = new Thread(AcceptLoop);
        _acceptThread.IsBackground = true;
        _acceptThread.Start();
    }


    public void Stop()
    {
        _serverStopped = true;

        // Interrupt then stop so that Exception is handled correctly
        _acceptThread?.Interrupt();
        Listener?.Stop();
        _acceptThread = null;


        SimpleWebLog.Info("Server stoped, Closing all connections...");

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
                    TcpClient client = Listener.AcceptTcpClient();
                    _tcpConfig.ApplyTo(client);


                    // TODO keep track of connections before they are in connections dictionary
                    //      this might not be a problem as HandshakeAndReceiveLoop checks for stop
                    //      and returns/disposes before sending message to queue
                    Common.Connection conn = new(client, AfterConnectionDisposed);

                    //SimpleWebLog.Info($"A client connected {conn}");

                    // handshake needs its own thread as it needs to wait for message from client
                    Thread receiveThread = new(() => HandshakeAndReceiveLoop(conn));

                    conn.receiveThread = receiveThread;

                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
            }
            catch (SocketException)
            {
                // check for Interrupted/Abort
                Utils.CheckForInterupt();
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
                //SimpleWebLog.Error($"Failed to create SSL Stream {conn}");
                conn.Dispose();
                return;
            }

            success = _handShake.TryHandshake(conn);

            if (success)
            {
                //SimpleWebLog.Info($"Sent Handshake {conn}");
            }
            else
            {
                //SimpleWebLog.Error($"Handshake Failed {conn}");
                conn.Dispose();
                return;
            }

            // check if Stop has been called since accepting this client
            if (_serverStopped)
            {
                SimpleWebLog.Info("Server stops after successful handshake");
                return;
            }

            bool isIdAvailable = _availableSessionIds.TryTake(out uint uId);

            if (!isIdAvailable)
            {
                SimpleWebLog.Warn("Ran out of available session IDs. A client attempting to connect will be rejected.");
                conn.Dispose();
                return;
            }

            conn.connId = new SessionId(uId);

            _connections.TryAdd(conn.connId, conn);

            ReceiveQueue.Enqueue(new Message(conn.connId, EventType.Connected));

            Thread sendThread = new(
                () =>
                {
                    SendLoop.Config sendConfig = new(
                        conn,
                        Constants.HeaderSize + _maxMessageSize,
                        false);

                    SendLoop.Loop(sendConfig);
                });

            conn.sendThread = sendThread;
            sendThread.IsBackground = true;
            sendThread.Name = $"SendLoop {conn.connId}";
            sendThread.Start();

            ReceiveLoop.Config receiveConfig = new(
                conn,
                _maxMessageSize,
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
            // close here incase connect fails
            conn.Dispose();
        }
    }


    private void AfterConnectionDisposed(Common.Connection conn)
    {
        if (conn.connId != SessionId.Invalid)
        {
            ReceiveQueue.Enqueue(new Message(conn.connId, EventType.Disconnected));
            _connections.TryRemove(conn.connId, out Common.Connection _);
            _availableSessionIds.Add(conn.connId.Value);
        }
    }


    public void Send(SessionId id, ArrayBuffer buffer)
    {
        if (_connections.TryGetValue(id, out Common.Connection? conn))
        {
            conn.sendQueue.Enqueue(buffer);
            conn.sendPending.Set();
        }
        else
            SimpleWebLog.Warn($"Cant send message to {id} because connection was not found in dictionary. Maybe it disconnected.");
    }


    public bool CloseConnection(SessionId id)
    {
        if (_connections.TryGetValue(id, out Common.Connection? conn))
        {
            SimpleWebLog.Info($"Kicking connection {id}");
            conn.Dispose();
            return true;
        }
        else
        {
            SimpleWebLog.Warn($"Failed to kick {id} because id not found");

            return false;
        }
    }


    public EndPoint? GetClientEndPoint(SessionId id)
    {
        if (_connections.TryGetValue(id, out Common.Connection? conn))
            return conn.client.Client.RemoteEndPoint;

        SimpleWebLog.Error($"Cant get endpoint of connection to {id} because connection was not found in dictionary");
        return null;
    }
}