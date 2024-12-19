using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Client;
using ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;
using ScaleNet.Common;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.Core
{
    public class ClientSocket : IDisposable
    {
        private string _address = string.Empty;
        private ushort _port;
        private SimpleWebClient _client;
        
        private readonly Queue<NetMessagePacket> _outgoing = new();

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        
        public event Action<ConnectionStateArgs>? ClientStateChanged;
        public event Action<ArraySegment<byte>>? MessageReceived;


        public void Dispose()
        {
            StopConnection();
        }

        
        /// <summary>
        /// Threaded operation to process client actions.
        /// </summary>
        private void Socket(bool useWss)
        {

            TcpConfig tcpConfig = new TcpConfig(false, 5000, 20000);
            _client = SimpleWebClient.Create(ushort.MaxValue, 5000, tcpConfig);

            _client.onConnect += _client_onConnect;
            _client.onDisconnect += _client_onDisconnect;
            _client.onData += _client_onData;
            _client.onError += _client_onError;

            string scheme = (useWss) ? "wss" : "ws";
            UriBuilder builder = new UriBuilder
            {
                Scheme = scheme,
                Host = _address,
                Port = _port
            };
            SetConnectionState(ConnectionState.Starting, false);
            _client.Connect(builder.Uri);
        }

        private void _client_onError(Exception obj)
        {
            StopConnection();
        }

        private void _client_onData(ArraySegment<byte> data)
        {
            if (_client == null || _client.ConnectionState != ClientState.Connected)
                return;

            Channel channel;
            data = RemoveChannel(data, out channel);
            ClientReceivedDataArgs dataArgs = new ClientReceivedDataArgs(data, channel, Transport.Index);
            Transport.HandleClientReceivedDataArgs(dataArgs);
        }

        private void _client_onDisconnect()
        {
            StopConnection();
        }

        private void _client_onConnect()
        {
            SetConnectionState(ConnectionState.Started, false);
        }


        /// <summary>
        /// Starts the client connection.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="channelsCount"></param>
        /// <param name="pollTime"></param>
        internal bool StartConnection(string address, ushort port, bool useWss)
        {
            if (GetConnectionState() != ConnectionState.Stopped)
                return false;

            SetConnectionState(ConnectionState.Starting, false);
            //Assign properties.
            _port = port;
            _address = address;

            ResetQueues();
            Socket(useWss);

            return true;
        }


        /// <summary>
        /// Stops the local socket.
        /// </summary>
        internal bool StopConnection()
        {
            if (GetConnectionState() == ConnectionState.Stopped || GetConnectionState() == ConnectionState.Stopping)
                return false;

            SetConnectionState(ConnectionState.Stopping, false);
            _client.Disconnect();
            SetConnectionState(ConnectionState.Stopped, false);
            return true;
        }

        /// <summary>
        /// Resets queues.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetQueues()
        {
            ClearPacketQueue(ref _outgoing);
        }


        /// <summary>
        /// Dequeues and processes outgoing.
        /// </summary>
        private void DequeueOutgoing()
        {
            int count = _outgoing.Count;
            for (int i = 0; i < count; i++)
            {
                Packet outgoing = _outgoing.Dequeue();
                AddChannel(ref outgoing);
                _client.Send(outgoing.GetArraySegment());
                outgoing.Dispose();
            }
        }

        /// <summary>
        /// Allows for Outgoing queue to be iterated.
        /// </summary>
        internal void IterateOutgoing()
        {
            DequeueOutgoing();
        }

        /// <summary>
        /// Iterates the Incoming queue.
        /// </summary>
        internal void IterateIncoming()
        {
            if (_client == null)
                return;

            /* This has to be called even if not connected because it will also poll events such as
             * Connected, or Disconnected, ect. */
            _client.ProcessMessageQueue();
        }

        /// <summary>
        /// Sends a packet to the server.
        /// </summary>
        internal void SendToServer(NetMessagePacket packet)
        {
            //Not started, cannot send.
            if (GetConnectionState() != ConnectionState.Started)
                return;

            Send(ref _outgoing, channelId, segment, -1);
        }


        /// <summary>
        /// Returns the current ConnectionState.
        /// </summary>
        /// <returns></returns>
        internal ConnectionState GetConnectionState()
        {
            return State;
        }


        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        protected void SetConnectionState(ConnectionState connectionState, bool asServer)
        {
            //If state hasn't changed.
            if (connectionState == State)
                return;

            State = connectionState;
            if (asServer)
                Transport.HandleServerConnectionState(new ServerConnectionStateArgs(connectionState, Transport.Index));
            else
                Transport.HandleClientConnectionState(new ClientConnectionStateArgs(connectionState, Transport.Index));
        }


        /// <summary>
        /// Sends data to connectionId.
        /// </summary>
        internal void Send(ref Queue<Packet> queue, byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (GetConnectionState() != ConnectionState.Started)
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


        /// <summary>
        /// Adds channel to the end of the data.
        /// </summary>
        internal void AddChannel(ref Packet packet)
        {
            int writePosition = packet.Length;
            packet.AddLength(1);
            packet.Data[writePosition] = (byte)packet.Channel;
        }


        /// <summary>
        /// Removes the channel, outputting it and returning a new ArraySegment.
        /// </summary>
        internal ArraySegment<byte> RemoveChannel(ArraySegment<byte> segment, out Channel channel)
        {
            byte[] array = segment.Array;
            int count = segment.Count;

            channel = (Channel)array[count - 1];
            return new ArraySegment<byte>(array, 0, count - 1);
        }
    }
}
