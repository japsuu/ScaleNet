using System;
using ScaleNet.Common;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket
{
    public class WebSocketClientTransport : IClientTransport
    {
        public string Address { get; set; }
        public int Port { get; set; }
        
        public event Action<ConnectionStateArgs>? ConnectionStateChanged;
        public event Action<DeserializedNetMessage>? MessageReceived;
        
        
        public void Dispose()
        {
            throw new NotImplementedException();
        }
        

        public void ConnectClient()
        {
            throw new NotImplementedException();
        }


        public void ReconnectClient()
        {
            throw new NotImplementedException();
        }


        public void DisconnectClient()
        {
            throw new NotImplementedException();
        }


        public void SendAsync<T>(T message) where T : INetMessage
        {
            throw new NotImplementedException();
        }
    }
}