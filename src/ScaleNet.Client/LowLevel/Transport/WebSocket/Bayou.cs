using System;
using System.Runtime.CompilerServices;
using ScaleNet.Client.LowLevel.Transport.WebSocket.Core;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket
{
    public class Bayou
    {
        //Security.
        /// <summary>
        /// True to connect using WSS.
        /// </summary>
        private bool _useWss = false;

        /// <summary>
        /// Maximum transmission unit for this transport.
        /// </summary>
        private int _mtu = 1023;

        //Server.
        /// <summary>
        /// Port to use.
        /// </summary>
        private ushort _port = 7770;

        //Client.
        /// <summary>
        /// Address to connect.
        /// </summary>
        private string _clientAddress = "localhost";

        /// <summary>
        /// Client socket and handler.
        /// </summary>
        private ClientSocket _client = new ClientSocket();

        protected void OnDestroy()
        {
            StopClient();
        }

        /// <summary>
        /// Called when a connection state changes for the local client.
        /// </summary>
        public override event Action<ClientConnectionStateArgs> OnClientConnectionState;
        
        /// <summary>
        /// Called when client receives data.
        /// </summary>
        public override event Action<ClientReceivedDataArgs> OnClientReceivedData;
        
        /// <summary>
        /// Handles a ConnectionStateArgs for the local client.
        /// </summary>
        /// <param name="connectionStateArgs"></param>
        public override void HandleClientConnectionState(ClientConnectionStateArgs connectionStateArgs)
        {
            OnClientConnectionState?.Invoke(connectionStateArgs);
        }
        
        
        public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs receivedDataArgs)
        {
            OnClientReceivedData?.Invoke(receivedDataArgs);
        }
        
        
        public override LocalConnectionState GetConnectionState()
        {
            return _client.GetConnectionState();
        }
        

        /// <summary>
        /// Processes data received by the socket.
        /// </summary>
        public override void IterateIncoming()
        {
            _client.IterateIncoming();
        }

        
        public override void IterateOutgoing()
        {
            _client.IterateOutgoing();
        }

        
        /// <summary>
        /// Sends to the server or all clients.
        /// </summary>
        /// <param name="channelId">Channel to use.</param>
        /// <param name="segment">Data to send.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void SendToServer(byte channelId, ArraySegment<byte> segment)
        {
            _client.SendToServer(channelId, segment);
        }

        /// <summary>
        /// Starts the client.
        /// </summary>
        /// <param name="address"></param>
        private bool StartClient(string address)
        {
            _client.Initialize(this, _mtu);
            return _client.StartConnection(address, _port, _useWss);
        }

        /// <summary>
        /// Stops the client.
        /// </summary>
        private bool StopClient()
        {
            return _client.StopConnection();
        }
    }
}
