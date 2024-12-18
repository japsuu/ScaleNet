using JamesFrowen.SimpleWeb;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ScaleNet.Server;
using ScaleNet.Server.LowLevel;
using ScaleNet.Server.LowLevel.Transport.WebSocket;

namespace FishNet.Transporting.Bayou.Server
{
    public class ServerSocket
    {
        /// <summary>
        /// Transport controlling this socket.
        /// </summary>
        private WebSocketServerTransport Transport = null;
        /// <summary>
        /// Current ConnectionState.
        /// </summary>
        private ServerState _connectionState = ServerState.Stopped;
        /// <summary>
        /// Returns the current ServerState.
        /// </summary>
        /// <returns></returns>
        internal ServerState GetConnectionState()
        {
            return _connectionState;
        }
        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        protected void SetConnectionState(ServerState connectionState)
        {
            //If state hasn't changed.
            if (connectionState == _connectionState)
                return;

            ServerState oldState = _connectionState;
            _connectionState = connectionState;
            Transport.HandleServerConnectionState(new ServerStateChangeArgs(connectionState, oldState));
        }

        /// <summary>
        /// Sends data to connectionId.
        /// </summary>
        internal void Send(ref Queue<Packet> queue, byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (GetConnectionState() != ServerState.Started)
                return;

            //ConnectionId isn't used from client to server.
            Packet outgoing = new Packet(connectionId, segment, channelId);
            queue.Enqueue(outgoing);
        }

        /// <summary>
        /// Clears a queue using Packet type.
        /// </summary>
        /// <param name="queue"></param>
        internal void ClearPacketQueue(ref Queue<Packet> queue)
        {
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                Packet p = queue.Dequeue();
                p.Dispose();
            }
        }

        #region Public.
        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        internal ConnectionState GetConnectionState(SessionId connectionId)
        {
            ConnectionState state = _clients.Contains(connectionId) ? ConnectionState.Connected : ConnectionState.Disconnected;
            return state;
        }
        #endregion

        #region Private.
        #region Configuration.
        /// <summary>
        /// Port used by server.
        /// </summary>
        private ushort _port;
        /// <summary>
        /// Maximum number of allowed clients.
        /// </summary>
        private int _maximumClients;
        /// <summary>
        /// MTU sizes for each channel.
        /// </summary>
        private int _mtu;
        #endregion
        #region Queues.
        /// <summary>
        /// Outbound messages which need to be handled.
        /// </summary>
        private Queue<Packet> _outgoing = new Queue<Packet>();
        /// <summary>
        /// Ids to disconnect next iteration. This ensures data goes through to disconnecting remote connections. This may be removed in a later release.
        /// </summary>
        private List<int> _disconnectingNext = new List<int>();
        /// <summary>
        /// Ids to disconnect immediately.
        /// </summary>
        private List<int> _disconnectingNow = new List<int>();
        #endregion
        /// <summary>
        /// Currently connected clients.
        /// </summary>
        private HashSet<int> _clients = new HashSet<int>();
        /// <summary>
        /// Server socket manager.
        /// </summary>
        private SimpleWebServer _server;
        /// <summary>
        /// SslConfiguration to use.
        /// </summary>
        private ServerSslContext _sslContext;
        #endregion

        ~ServerSocket()
        {
            StopConnection();
        }

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        internal void Initialize(WebSocketServerTransport t, int unreliableMTU, ServerSslContext context)
        {
            _sslContext = context;
            Transport = t;
            _mtu = unreliableMTU;
        }

        /// <summary>
        /// Threaded operation to process server actions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Socket(int maxClients)
        {
            TcpConfig tcpConfig = new TcpConfig(false, 5000, 20000);
            SslConfig config;
            if (!_sslContext.Enabled)
                config = new SslConfig();
            else
                config = new SslConfig(_sslContext.Enabled, _sslContext.CertificatePath, _sslContext.CertificatePassword,
                    _sslContext.SslProtocol);
            _server = new SimpleWebServer(maxClients, 5000, tcpConfig, _mtu, 5000, config);

            _server.onConnect += _server_onConnect;
            _server.onDisconnect += _server_onDisconnect;
            _server.onData += _server_onData;
            _server.onError += _server_onError;

            base.SetConnectionState(LocalConnectionState.Starting, true);
            _server.Start(_port);
            base.SetConnectionState(LocalConnectionState.Started, true);
        }

        /// <summary>
        /// Called when a client connection errors.
        /// </summary>
        private void _server_onError(int connectionId, Exception arg2)
        {
            ConnectionStoppedOnSocket(connectionId);
        }

        /// <summary>
        /// Called when a connection has stopped on a socket level.
        /// </summary>
        /// <param name="connectionId"></param>
        private void ConnectionStoppedOnSocket(int connectionId) 
        {
            if (_clients.Remove(connectionId))
                base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(ConnectionState.Stopped, connectionId, base.Transport.Index));
        }

        /// <summary>
        /// Called when receiving data.
        /// </summary>
        private void _server_onData(int clientId, ArraySegment<byte> data)
        {
            if (_server == null || !_server.Active)
                return;

            Channel channel;
            ArraySegment<byte> segment = base.RemoveChannel(data, out channel);

            ServerReceivedDataArgs dataArgs = new ServerReceivedDataArgs(segment, channel, clientId, base.Transport.Index);
            base.Transport.HandleServerReceivedDataArgs(dataArgs);
        }

        /// <summary>
        /// Called when a client connects.
        /// </summary>
        private void _server_onConnect(int clientId)
        {
            if (_server == null || !_server.Active)
                return;

            if (_clients.Count >= _maximumClients)
            {
                _server.KickClient(clientId);
                return;
            }

            _clients.Add(clientId);
            ConnectionState state = ConnectionState.Started;
            base.Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(state, clientId, base.Transport.Index));
        }

        /// <summary>
        /// Called when a client disconnects.
        /// </summary>
        private void _server_onDisconnect(int connectionId)
        {
            ConnectionStoppedOnSocket(connectionId);
        }

        /// <summary>
        /// Gets the address of a remote connection Id.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns>Returns string.empty if Id is not found.</returns>
        internal EndPoint? GetConnectionAddress(SessionId connectionId)
        {
            if (_server == null || !_server.Active)
                return null;

            return _server.GetClientAddress(connectionId);
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        internal bool StartConnection(ushort port, int maximumClients)
        {
            if (base.GetConnectionState() != LocalConnectionState.Stopped)
                return false;

            base.SetConnectionState(LocalConnectionState.Starting, true);

            //Assign properties.
            _port = port;
            _maximumClients = maximumClients;
            ResetQueues();
            Socket(maximumClients);
            return true;
        }

        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            if (_server == null || base.GetConnectionState() == LocalConnectionState.Stopped || base.GetConnectionState() == LocalConnectionState.Stopping)
                return false;

            ResetQueues();
            base.SetConnectionState(LocalConnectionState.Stopping, true);
            _server.Stop();
            base.SetConnectionState(LocalConnectionState.Stopped, true);

            return true;
        }

        /// <summary>
        /// Stops a remote client disconnecting the client from the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId of the client to disconnect.</param>
        internal bool StopConnection(int connectionId, bool immediately)
        {
            if (_server == null || base.GetConnectionState() != LocalConnectionState.Started)
                return false;

            //Don't disconnect immediately, wait until next command iteration.
            if (!immediately)
                _disconnectingNext.Add(connectionId);
            //Disconnect immediately.
            else
                _server.KickClient(connectionId);

            return true;
        }
        
        /// <summary>
        /// Resets queues.
        /// </summary>
        private void ResetQueues()
        {
            _clients.Clear();
            base.ClearPacketQueue(ref _outgoing);
            _disconnectingNext.Clear();
            _disconnectingNow.Clear();
        }

        /// <summary>
        /// Dequeues and processes commands.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DequeueDisconnects()
        {
            int count;

            count = _disconnectingNow.Count;
            //If there are disconnect nows.
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                    StopConnection(_disconnectingNow[i], true);

                _disconnectingNow.Clear();
            }

            count = _disconnectingNext.Count;
            //If there are disconnect next.
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                    _disconnectingNow.Add(_disconnectingNext[i]);

                _disconnectingNext.Clear();
            }
        }

        /// <summary>
        /// Dequeues and processes outgoing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DequeueOutgoing()
        {
            if (base.GetConnectionState() != LocalConnectionState.Started || _server == null)
            {
                //Not started, clear outgoing.
                base.ClearPacketQueue(ref _outgoing);
            }
            else
            {
                int count = _outgoing.Count;
                for (int i = 0; i < count; i++)
                {
                    Packet outgoing = _outgoing.Dequeue();
                    int connectionId = outgoing.ConnectionId;
                    AddChannel(ref outgoing);
                    ArraySegment<byte> segment = outgoing.GetArraySegment();

                    //Send to all clients.
                    if (connectionId == -1)
                        _server.SendAll(_clients, segment);
                    //Send to one client.
                    else
                        _server.SendOne(connectionId, segment);

                    outgoing.Dispose();
                }
            }
        }

        /// <summary>
        /// Allows for Outgoing queue to be iterated.
        /// </summary>
        internal void IterateOutgoing()
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
        internal void IterateIncoming()
        {
            if (_server == null)
                return;

            /* Read socket messages. Can contain
            * connect, data, disconnect, error messages. */
            _server.ProcessMessageQueue();
        }

        /// <summary>
        /// Sends a packet to a single, or all clients.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            Send(ref _outgoing, channelId, segment, connectionId);
        }

        /// <summary>
        /// Returns the maximum number of clients allowed to connect to the server. If the transport does not support this method the value -1 is returned.
        /// </summary>
        /// <returns></returns>
        internal int GetMaximumClients()
        {
            return _maximumClients;
        }
    }
}
