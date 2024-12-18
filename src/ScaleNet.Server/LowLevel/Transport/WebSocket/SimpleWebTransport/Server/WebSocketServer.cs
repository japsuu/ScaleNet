using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

public class WebSocketServer
{
    public readonly ConcurrentQueue<Message> receiveQueue = new ConcurrentQueue<Message>();

    readonly TcpConfig tcpConfig;
    readonly int maxMessageSize;

    public TcpListener listener;
    Thread acceptThread;
    bool serverStopped;
    private readonly int _maxClients;
    readonly ServerHandshake handShake;
    readonly ServerSslHelper sslHelper;
    readonly BufferPool bufferPool;
    readonly ConcurrentDictionary<SessionId, Common.Connection> connections = new ConcurrentDictionary<SessionId, Common.Connection>();


    private readonly ConcurrentBag<uint> _availableSessionIds = [];

    public WebSocketServer(int maxClients, TcpConfig tcpConfig, int maxMessageSize, int handshakeMaxSize, SslConfig sslConfig, BufferPool bufferPool)
    {
        _maxClients = maxClients;
        this.tcpConfig = tcpConfig;
        this.maxMessageSize = maxMessageSize;
        sslHelper = new ServerSslHelper(sslConfig);
        this.bufferPool = bufferPool;
        handShake = new ServerHandshake(this.bufferPool, handshakeMaxSize);
        
        // Fill the available session IDs bag.
        for (uint i = 1; i < maxClients; i++)
            _availableSessionIds.Add(i);
    }

    public void Listen(int port)
    {
        listener = TcpListener.Create(port);
        listener.Start();
        SimpleWebLog.Info($"Server has started on port {port}");

        acceptThread = new Thread(acceptLoop);
        acceptThread.IsBackground = true;
        acceptThread.Start();
    }

    public void Stop()
    {
        serverStopped = true;

        // Interrupt then stop so that Exception is handled correctly
        acceptThread?.Interrupt();
        listener?.Stop();
        acceptThread = null;


        SimpleWebLog.Info("Server stoped, Closing all connections...");
        // make copy so that foreach doesn't break if values are removed
        Common.Connection[] connectionsCopy = connections.Values.ToArray();
        foreach (Common.Connection conn in connectionsCopy)
        {
            conn.Dispose();
        }

        connections.Clear();
    }

    void acceptLoop()
    {
        try
        {
            try
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    tcpConfig.ApplyTo(client);


                    // TODO keep track of connections before they are in connections dictionary
                    //      this might not be a problem as HandshakeAndReceiveLoop checks for stop
                    //      and returns/disposes before sending message to queue
                    Common.Connection conn = new Common.Connection(client, AfterConnectionDisposed);
                    //SimpleWebLog.Info($"A client connected {conn}");

                    // handshake needs its own thread as it needs to wait for message from client
                    Thread receiveThread = new Thread(() => HandshakeAndReceiveLoop(conn));

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
        catch (ThreadInterruptedException e) { SimpleWebLog.InfoException(e); }
        catch (ThreadAbortException e) { SimpleWebLog.InfoException(e); }
        catch (Exception e) { SimpleWebLog.Exception(e); }
    }

    void HandshakeAndReceiveLoop(Common.Connection conn)
    {
        try
        {
            bool success = sslHelper.TryCreateStream(conn);
            if (!success)
            {
                //SimpleWebLog.Error($"Failed to create SSL Stream {conn}");
                conn.Dispose();
                return;
            }

            success = handShake.TryHandshake(conn);

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
            if (serverStopped)
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

            connections.TryAdd(conn.connId, conn);

            receiveQueue.Enqueue(new Message(conn.connId, EventType.Connected));

            Thread sendThread = new Thread(() =>
            {
                SendLoop.Config sendConfig = new SendLoop.Config(
                    conn,
                    bufferSize: Constants.HeaderSize + maxMessageSize,
                    setMask: false);

                SendLoop.Loop(sendConfig);
            });

            conn.sendThread = sendThread;
            sendThread.IsBackground = true;
            sendThread.Name = $"SendLoop {conn.connId}";
            sendThread.Start();

            ReceiveLoop.Config receiveConfig = new ReceiveLoop.Config(
                conn,
                maxMessageSize,
                expectMask: true,
                receiveQueue,
                bufferPool);

            ReceiveLoop.Loop(receiveConfig);
        }
        catch (ThreadInterruptedException e) { SimpleWebLog.InfoException(e); }
        catch (ThreadAbortException e) { SimpleWebLog.InfoException(e); }
        catch (Exception e) { SimpleWebLog.Exception(e); }
        finally
        {
            // close here incase connect fails
            conn.Dispose();
        }
    }

    void AfterConnectionDisposed(Common.Connection conn)
    {
        if (conn.connId != SessionId.Invalid)
        {
            receiveQueue.Enqueue(new Message(conn.connId, EventType.Disconnected));
            connections.TryRemove(conn.connId, out Common.Connection _);
            _availableSessionIds.Add(conn.connId.Value);
        }
    }

    public void Send(SessionId id, ArrayBuffer buffer)
    {
        if (connections.TryGetValue(id, out Common.Connection? conn))
        {
            conn.sendQueue.Enqueue(buffer);
            conn.sendPending.Set();
        }
        else
        {
            SimpleWebLog.Warn($"Cant send message to {id} because connection was not found in dictionary. Maybe it disconnected.");
        }
    }

    public bool CloseConnection(SessionId id)
    {
        if (connections.TryGetValue(id, out Common.Connection? conn))
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
        if (connections.TryGetValue(id, out Common.Connection? conn))
        {
            return conn.client.Client.RemoteEndPoint;
        }

        SimpleWebLog.Error($"Cant get endpoint of connection to {id} because connection was not found in dictionary");
        return null;
    }
}