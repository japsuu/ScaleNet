using System;
using ScaleNet.Client.LowLevel.Transport.WebSocket.Core;
using ScaleNet.Common;
using ScaleNet.Common.LowLevel;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket
{
    public class WebSocketClientTransport : IClientTransport
    {
        private readonly IPacketMiddleware? _middleware;
        private readonly ClientSocket _clientSocket;
        
        public string Address { get; }
        public ushort Port { get; }
        public ConnectionState State => _clientSocket.State;
        
        public event Action<ConnectionStateArgs>? ConnectionStateChanged;
        public event Action<DeserializedNetMessage>? MessageReceived;


        public WebSocketClientTransport(string address, ushort port, ClientSslContext? sslContext, IPacketMiddleware? middleware = null)
        {
            Address = address;
            Port = port;

            _middleware = middleware;
            
            _clientSocket = new ClientSocket(sslContext);
            _clientSocket.ClientStateChanged += OnClientStateChanged;
            _clientSocket.MessageReceived += OnClientReceivedData;
        }


        private void OnClientStateChanged(ConnectionStateArgs args)
        {
            try
            {
                ConnectionStateChanged?.Invoke(args);
            }
            catch (Exception e)
            {
                ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(ConnectionStateChanged)} event:\n{e}");
                throw;
            }
        }


        private void OnClientReceivedData(ArraySegment<byte> data)
        {
            NetMessagePacket packet = NetMessagePacket.CreateIncomingNoCopy(data, false);
        
            _middleware?.HandleIncomingPacket(ref packet);
        
            bool serializeSuccess = NetMessages.TryDeserialize(packet, out DeserializedNetMessage msg);
                
            if (!serializeSuccess)
            {
                ScaleNetManager.Logger.LogWarning($"Received a packet that could not be deserialized.");
                return;
            }

            try
            {
                MessageReceived?.Invoke(msg);
            }
            catch (Exception e)
            {
                ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(MessageReceived)} event:\n{e}");
                throw;
            }
        }
        
        
        public void Dispose()
        {
            DisconnectClient();
        
            _clientSocket.Dispose();
        }
        

        public bool ConnectClient()
        {
            return _clientSocket.StartConnection(Address, Port);
        }


        public bool ReconnectClient()
        {
            return DisconnectClient() && ConnectClient();
        }


        public bool DisconnectClient()
        {
            return _clientSocket.StopConnection();
        }


        public void IterateIncoming()
        {
            _clientSocket.IterateIncoming();
        }


        public void IterateOutgoing()
        {
            _clientSocket.IterateOutgoing();
        }


        public void SendAsync<T>(T message) where T : INetMessage
        {
            // Write to a packet.
            if (!NetMessages.TrySerialize(message, out NetMessagePacket packet))
                return;

            _clientSocket.SendToServer(packet);
        }
    }
}