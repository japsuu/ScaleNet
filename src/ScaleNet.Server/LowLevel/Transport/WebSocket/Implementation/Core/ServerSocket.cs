using System.Net;
using System.Runtime.CompilerServices;
using ScaleNet.Common;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.Core;

internal sealed class ServerSocket : IDisposable
{
    /// <summary>
    /// A packet of data to send or receive from a remote connection.
    /// </summary>
    private readonly struct ConnectionPacket : IDisposable
    {
        public readonly ConnectionId ConnectionId;
        public readonly NetMessagePacket Payload;

        
        public ConnectionPacket(ConnectionId connectionId, NetMessagePacket payload)
        {
            ConnectionId = connectionId;
            Payload = payload;
        }


        public void Dispose()
        {
            Payload.Dispose();
        }
    }

    private ushort _port;
    private int _maximumClients;
    private SimpleWebServer? _server;
    
    private readonly int _maxPacketSize;
    private readonly ServerSslContext? _sslContext;
    /// <summary>
    /// Ids to disconnect the next iteration.
    /// This ensures data goes through to disconnecting remote connections.
    /// </summary>
    private readonly List<ConnectionId> _clientsAwaitingDisconnectDelayed = [];
    private readonly List<ConnectionId> _clientsAwaitingDisconnect = [];
    private readonly HashSet<ConnectionId> _connectedClients = [];
    private readonly Queue<ConnectionPacket> _outgoingPackets = new();

    public IReadOnlyCollection<ConnectionId> ConnectedClients => _connectedClients;
    public ServerState State { get; private set; } = ServerState.Stopped;
    
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    public event Action<SessionStateChangeArgs>? SessionStateChanged;
    public event Action<ConnectionId, ArraySegment<byte>>? DataReceived;


    public ServerSocket(int maxPacketSize, ServerSslContext? context)
    {
        _maxPacketSize = maxPacketSize;
        _sslContext = context;
    }
    
    
    public void Dispose()
    {
        StopServer();
    }


    /// <summary>
    /// Threaded operation to process server actions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeSocket(int maxClients)
    {
        TcpConfig tcpConfig = new(false, 5000, 20000);

        _server = new SimpleWebServer(maxClients, 10000, tcpConfig, _maxPacketSize, 5000, _sslContext);

        _server.OnConnect += OnClientConnect;
        _server.OnDisconnect += OnClientDisconnect;
        _server.OnData += OnReceiveDataFromClient;
        _server.OnError += OnClientError;

        SetServerState(ServerState.Starting);
        _server.Start(_port);
        SetServerState(ServerState.Started);
    }


    private void OnClientError(ConnectionId connectionId, Exception arg2)
    {
        ConnectionStoppedOnSocket(connectionId);
    }


    /// <summary>
    /// Called when a connection has stopped on a socket level.
    /// </summary>
    /// <param name="connectionId"></param>
    private void ConnectionStoppedOnSocket(ConnectionId connectionId)
    {
        if (_connectedClients.Remove(connectionId))
            SessionStateChanged?.Invoke(new SessionStateChangeArgs(connectionId, ConnectionState.Disconnected));
    }


    /// <summary>
    /// Called when receiving data.
    /// </summary>
    private void OnReceiveDataFromClient(ConnectionId clientId, ArraySegment<byte> data)
    {
        if (_server == null || !_server.Active)
            return;

        DataReceived?.Invoke(clientId, data);
    }


    /// <summary>
    /// Called when a client connects.
    /// </summary>
    private void OnClientConnect(ConnectionId clientId)
    {
        if (_server == null || !_server.Active)
            return;

        if (_connectedClients.Count >= _maximumClients)
        {
            _server.KickClient(clientId);
            return;
        }

        _connectedClients.Add(clientId);
        SessionStateChanged?.Invoke(new SessionStateChangeArgs(clientId, ConnectionState.Connected));
    }


    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    private void OnClientDisconnect(ConnectionId connectionId)
    {
        ConnectionStoppedOnSocket(connectionId);
    }


    /// <summary>
    /// Gets the current ConnectionState of a remote client on the server.
    /// </summary>
    /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
    public ConnectionState GetConnectionState(ConnectionId connectionId)
    {
        ConnectionState state = _connectedClients.Contains(connectionId) ? ConnectionState.Connected : ConnectionState.Disconnected;
        return state;
    }


    /// <summary>
    /// Gets the address of a remote connection Id.
    /// </summary>
    /// <param name="connectionId"></param>
    /// <returns>Returns string.empty if Id is not found.</returns>
    public EndPoint? GetConnectionEndPoint(ConnectionId connectionId)
    {
        if (_server == null || !_server.Active)
            return null;

        return _server.GetClientAddress(connectionId);
    }


    /// <summary>
    /// Starts the server.
    /// </summary>
    public bool StartServer(ushort port, int maximumClients)
    {
        if (State != ServerState.Stopped)
            return false;

        SetServerState(ServerState.Starting);

        _port = port;
        _maximumClients = maximumClients;
        ResetQueues();
        InitializeSocket(maximumClients);
        return true;
    }


    /// <summary>
    /// Stops the local socket.
    /// </summary>
    public bool StopServer()
    {
        if (_server == null || State == ServerState.Stopped || State == ServerState.Stopping)
            return false;

        ResetQueues();
        SetServerState(ServerState.Stopping);
        _server.Stop();
        SetServerState(ServerState.Stopped);

        return true;
    }


    /// <summary>
    /// Stops a remote client, disconnecting it from the server.
    /// </summary>
    public bool StopConnection(ConnectionId connectionId, bool iterateOutgoing)
    {
        if (_server == null || State != ServerState.Started)
            return false;

        if (iterateOutgoing)
            _clientsAwaitingDisconnectDelayed.Add(connectionId);
        else
            _server.KickClient(connectionId);

        return true;
    }


    /// <summary>
    /// Resets queues.
    /// </summary>
    private void ResetQueues()
    {
        _connectedClients.Clear();
        ClearPacketQueues();
        _clientsAwaitingDisconnectDelayed.Clear();
        _clientsAwaitingDisconnect.Clear();
    }


    /// <summary>
    /// Dequeues and processes commands.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DequeueDisconnects()
    {
        int count;

        count = _clientsAwaitingDisconnect.Count;

        //If there are disconnect nows.
        if (count > 0)
        {
            for (int i = 0; i < count; i++)
                StopConnection(_clientsAwaitingDisconnect[i], true);

            _clientsAwaitingDisconnect.Clear();
        }

        count = _clientsAwaitingDisconnectDelayed.Count;

        //If there are disconnect next.
        if (count > 0)
        {
            for (int i = 0; i < count; i++)
                _clientsAwaitingDisconnect.Add(_clientsAwaitingDisconnectDelayed[i]);

            _clientsAwaitingDisconnectDelayed.Clear();
        }
    }


    /// <summary>
    /// Dequeues and processes outgoing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DequeueOutgoing()
    {
        if (State != ServerState.Started || _server == null)
        {
            //Not started, clear outgoing.
            ClearPacketQueues();
        }
        else
        {
            int count = _outgoingPackets.Count;
            for (int i = 0; i < count; i++)
            {
                ConnectionPacket outgoing = _outgoingPackets.Dequeue();
                ConnectionId connectionId = outgoing.ConnectionId;

                if (connectionId == ConnectionId.Broadcast)
                    _server.SendAll(_connectedClients, outgoing.Payload.Buffer, outgoing.Payload.Offset, outgoing.Payload.Length);
                else
                    _server.SendOne(connectionId, outgoing.Payload.Buffer, outgoing.Payload.Offset, outgoing.Payload.Length);

                outgoing.Dispose();
            }
        }
    }


    /// <summary>
    /// Sends a packet to a single or all clients.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void QueueSend(ConnectionId connectionId, NetMessagePacket payload)
    {
        if (State != ServerState.Started)
            return;

        ConnectionPacket outgoing = new(connectionId, payload);
        _outgoingPackets.Enqueue(outgoing);
    }


    /// <summary>
    /// Allows for Outgoing queue to be iterated.
    /// </summary>
    public void IterateOutgoing()
    {
        if (_server == null)
            return;

        DequeueOutgoing();
        DequeueDisconnects();
    }


    /// <summary>
    /// Iterates the Incoming queue.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IterateIncoming()
    {
        /* Read socket messages. Can contain
         * connect, data, disconnect, error messages. */
        _server?.ProcessMessageQueue();
    }


    private void SetServerState(ServerState newState)
    {
        if (newState == State)
            return;

        ServerState oldState = State;
        State = newState;
        ServerStateChanged?.Invoke(new ServerStateChangeArgs(newState, oldState));
    }


    /// <summary>
    /// Clears a queue using ConnectionPacket type.
    /// </summary>
    private void ClearPacketQueues()
    {
        int count = _outgoingPackets.Count;
        for (int i = 0; i < count; i++)
        {
            ConnectionPacket p = _outgoingPackets.Dequeue();
            p.Dispose();
        }
    }
}