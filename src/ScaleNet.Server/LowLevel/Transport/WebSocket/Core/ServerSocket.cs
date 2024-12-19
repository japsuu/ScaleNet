using System.Net;
using System.Runtime.CompilerServices;
using ScaleNet.Common;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.Core;

internal sealed class ServerSocket : IDisposable
{
    /// <summary>
    /// A raw packet of data.
    /// </summary>
    private readonly struct Packet : IDisposable
    {
        public readonly SessionId SessionID;
        public readonly NetMessagePacket Payload;


        /// <summary>
        /// A raw packet of data.
        /// </summary>
        public Packet(SessionId sessionID, NetMessagePacket payload)
        {
            SessionID = sessionID;
            Payload = payload;
        }


        public void Dispose()
        {
            Payload.Dispose();
        }
    }

    private ushort _port;
    private int _maximumClients;
    private int _maxPacketSize;
    private SimpleWebServer? _server;
    private ServerSslContext? _sslContext;
    
    /// <summary>
    /// Ids to disconnect the next iteration.
    /// This ensures data goes through to disconnecting remote connections.
    /// </summary>
    private readonly List<SessionId> _clientsAwaitingDisconnectDelayed = [];
    private readonly List<SessionId> _clientsAwaitingDisconnect = [];
    private readonly HashSet<SessionId> _connectedClients = [];
    private readonly Queue<Packet> _outgoingPackets = new();

    public IReadOnlyCollection<SessionId> ConnectedClients => _connectedClients;
    public ServerState State { get; private set; } = ServerState.Stopped;
    
    public event Action<ServerStateChangeArgs>? ServerStateChanged;
    public event Action<SessionStateChangeArgs>? SessionStateChanged;
    public event Action<SessionId, ArraySegment<byte>>? MessageReceived;
    
    
    public void Dispose()
    {
        StopServer();
    }


    /// <summary>
    /// Initializes this for use.
    /// </summary>
    internal void Initialize(int maxPacketSize, ServerSslContext context)
    {
        _sslContext = context;
        _maxPacketSize = maxPacketSize;
    }


    /// <summary>
    /// Threaded operation to process server actions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeSocket(int maxClients)
    {
        TcpConfig tcpConfig = new(false, 5000, 20000);

        _server = new SimpleWebServer(maxClients, 10000, tcpConfig, _maxPacketSize, 5000, _sslContext);

        _server.OnConnect += _server_onConnect;
        _server.OnDisconnect += _server_onDisconnect;
        _server.OnData += _server_onData;
        _server.OnError += _server_onError;

        SetServerState(ServerState.Starting);
        _server.Start(_port);
        SetServerState(ServerState.Started);
    }


    /// <summary>
    /// Called when a client connection errors.
    /// </summary>
    private void _server_onError(SessionId connectionId, Exception arg2)
    {
        ConnectionStoppedOnSocket(connectionId);
    }


    /// <summary>
    /// Called when a connection has stopped on a socket level.
    /// </summary>
    /// <param name="connectionId"></param>
    private void ConnectionStoppedOnSocket(SessionId connectionId)
    {
        if (_connectedClients.Remove(connectionId))
            SessionStateChanged?.Invoke(new SessionStateChangeArgs(connectionId, ConnectionState.Disconnected));
    }


    /// <summary>
    /// Called when receiving data.
    /// </summary>
    private void _server_onData(SessionId clientId, ArraySegment<byte> data)
    {
        if (_server == null || !_server.Active)
            return;

        MessageReceived?.Invoke(clientId, data);
    }


    /// <summary>
    /// Called when a client connects.
    /// </summary>
    private void _server_onConnect(SessionId clientId)
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
    private void _server_onDisconnect(SessionId connectionId)
    {
        ConnectionStoppedOnSocket(connectionId);
    }


    /// <summary>
    /// Gets the current ConnectionState of a remote client on the server.
    /// </summary>
    /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
    public ConnectionState GetConnectionState(SessionId connectionId)
    {
        ConnectionState state = _connectedClients.Contains(connectionId) ? ConnectionState.Connected : ConnectionState.Disconnected;
        return state;
    }


    /// <summary>
    /// Gets the address of a remote connection Id.
    /// </summary>
    /// <param name="connectionId"></param>
    /// <returns>Returns string.empty if Id is not found.</returns>
    public EndPoint? GetConnectionEndPoint(SessionId connectionId)
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
    public bool StopConnection(SessionId connectionId, bool iterateOutgoing)
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
                Packet outgoing = _outgoingPackets.Dequeue();
                SessionId sessionId = outgoing.SessionID;

                if (sessionId == SessionId.Broadcast)
                    _server.SendAll(_connectedClients, outgoing.Payload.Buffer, outgoing.Payload.Length);
                else
                    _server.SendOne(sessionId, outgoing.Payload.Buffer, outgoing.Payload.Length);

                outgoing.Dispose();
            }
        }
    }


    /// <summary>
    /// Sends a packet to a single or all clients.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void QueueSend(SessionId connectionId, NetMessagePacket payload)
    {
        if (State != ServerState.Started)
            return;

        Packet outgoing = new(connectionId, payload);
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
    /// Clears a queue using Packet type.
    /// </summary>
    private void ClearPacketQueues()
    {
        int count = _outgoingPackets.Count;
        for (int i = 0; i < count; i++)
        {
            Packet p = _outgoingPackets.Dequeue();
            p.Dispose();
        }
    }
}