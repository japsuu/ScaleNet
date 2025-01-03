using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Client;
using ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;
using ScaleNet.Common;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.Core
{
    internal class ClientSocket : IDisposable
    {
        private string _address = string.Empty;
        private ushort _port;
        private SimpleWebClient? _client;

        private readonly ClientSslContext? _sslContext;
        private readonly Queue<NetMessagePacket> _outgoing;


        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public event Action<ConnectionStateArgs>? ClientStateChanged;
        public event Action<ArraySegment<byte>>? MessageReceived;


        public ClientSocket(ClientSslContext? sslContext)
        {
            _sslContext = sslContext;
            
            _outgoing = new Queue<NetMessagePacket>();
        }


        public void Dispose()
        {
            StopConnection();
        }


        public bool StartConnection(string address, ushort port)
        {
            if (State != ConnectionState.Disconnected)
                return false;

            SetConnectionState(ConnectionState.Connecting);

            _port = port;
            _address = address;

            ResetQueues();
            InitializeSocket();

            return true;
        }


        /// <summary>
        /// Stops the local socket.
        /// </summary>
        public bool StopConnection()
        {
            if (_client == null || State == ConnectionState.Disconnected || State == ConnectionState.Disconnecting)
                return false;

            SetConnectionState(ConnectionState.Disconnecting);
            _client.Disconnect();
            SetConnectionState(ConnectionState.Disconnected);
            
            return true;
        }


        /// <summary>
        /// Sends a packet to the server.
        /// </summary>
        public void SendToServer(NetMessagePacket packet)
        {
            if (State != ConnectionState.Connected)
                return;

            _outgoing.Enqueue(packet);
        }


        /// <summary>
        /// Allows for Outgoing queue to be iterated.
        /// </summary>
        public void IterateOutgoing()
        {
            DequeueOutgoing();
        }


        /// <summary>
        /// Iterates the Incoming queue.
        /// </summary>
        public void IterateIncoming()
        {
            /* This has to be called even if not connected because it will also poll events such as
             * Connected, or Disconnected, ect. */
            _client?.ProcessMessageQueue();
        }


        /// <summary>
        /// Threaded operation to process client actions.
        /// </summary>
        private void InitializeSocket()
        {
            // Originally 5000, 20000. Use larger values here to let the server handle timeouts.
            TcpConfig tcpConfig = new(false, 60000, 60000);
            _client = SimpleWebClient.Create(ushort.MaxValue, 5000, tcpConfig, _sslContext);

            _client.OnConnect += OnClientConnect;
            _client.OnDisconnect += OnClientDisconnect;
            _client.OnData += OnClientReceiveData;
            _client.OnError += OnClientError;

            bool useWss = _sslContext != null;
            string scheme = useWss ? "wss" : "ws";
            UriBuilder builder = new()
            {
                Scheme = scheme,
                Host = _address,
                Port = _port
            };
            SetConnectionState(ConnectionState.Connecting);
            _client.Connect(builder.Uri);
        }


        private void OnClientError(Exception e)
        {
            StopConnection();
        }


        private void OnClientReceiveData(ArraySegment<byte> data)
        {
            if (_client == null || _client.ConnectionState != ConnectionState.Connected)
                return;

            MessageReceived?.Invoke(data);
        }


        private void OnClientDisconnect()
        {
            StopConnection();
        }


        private void OnClientConnect()
        {
            SetConnectionState(ConnectionState.Connected);
        }


        /// <summary>
        /// Resets queues.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetQueues()
        {
            ClearPacketQueue();
        }


        /// <summary>
        /// Dequeues and processes outgoing.
        /// </summary>
        private void DequeueOutgoing()
        {
            if (_client == null || _client.ConnectionState != ConnectionState.Connected)
            {
                ClearPacketQueue();
                return;
            }
            
            int count = _outgoing.Count;
            for (int i = 0; i < count; i++)
            {
                NetMessagePacket outgoing = _outgoing.Dequeue();
                _client.Send(outgoing.Buffer, outgoing.Offset, outgoing.Length);
                outgoing.Dispose();
            }
        }
        
        
        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        private void SetConnectionState(ConnectionState connectionState)
        {
            if (connectionState == State)
                return;

            State = connectionState;
            ClientStateChanged?.Invoke(new ConnectionStateArgs(connectionState));
        }


        /// <summary>
        /// Clears a queue using NetMessagePacket type.
        /// </summary>
        private void ClearPacketQueue()
        {
            int count = _outgoing.Count;
            for (int i = 0; i < count; i++)
            {
                NetMessagePacket p = _outgoing.Dequeue();
                p.Dispose();
            }
        }
    }
}