using System;
using System.Collections.Generic;
using ScaleNet.Server;
using ScaleNet.Server.LowLevel.Transport.WebSocket;

namespace FishNet.Transporting.Bayou
{

    public abstract class SocketBase
    {

        #region Public.
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
        #endregion

        #region Protected.
        /// <summary>
        /// Transport controlling this socket.
        /// </summary>
        protected WebSocketServerTransport Transport = null;
        #endregion

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
    }

}