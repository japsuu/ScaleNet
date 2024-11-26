using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Threading.Tasks;
using ScaleNet.Common;
using ScaleNet.Common.Ssl;
using ScaleNet.Common.Transport.Tcp.Base.Core;
using ScaleNet.Common.Transport.Tcp.SSL.ByteMessage;
using ScaleNet.Common.Utils;

namespace ScaleNet.Client.LowLevel.Transport.Tcp
{
    public sealed class TcpClientTransport : IClientTransport
    {
        // Buffer for accumulating incomplete packet data
        private readonly SslByteMessageClient _client;
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        
        public string Address { get; set; }
        public int Port { get; set; }
    
        public event Action<ConnectionStateArgs>? ConnectionStateChanged;
        public event Action<DeserializedNetMessage>? MessageReceived;


        public TcpClientTransport(SslContext sslContext, string address, int port)
        {
            Address = address;
            Port = port;
            _client = new SslByteMessageClient(sslContext.Certificate);
            _client.RemoteCertificateValidationCallback = sslContext.CertificateValidationCallback;
            _client.GatherConfig = ScatterGatherConfig.UseBuffer;
            
            _client.OnBytesReceived += OnBytesReceived;
            _client.OnConnected += OnConnected;
            _client.OnDisconnected += OnDisconnected;
        }


        public void Connect()
        {
            _client.Connect(Address, Port);
            OnConnected();
        }


        public Task<bool> ConnectAsync()
        {
            return _client.ConnectAsyncAwaitable(Address, Port);
        }


        public void Disconnect()
        {
            _client.Disconnect();
        }


        public void SendAsync<T>(T message) where T : INetMessage
        {
            byte[] bytes = NetMessages.Serialize(message);
        
            if (bytes.Length > SharedConstants.MAX_PACKET_SIZE_BYTES)
            {
                ScaleNetManager.Logger.LogError($"Message {message} exceeds maximum packet size of {SharedConstants.MAX_PACKET_SIZE_BYTES} bytes. Skipping.");
                return;
            }
            
            if (!NetMessages.TryGetMessageId(message.GetType(), out ushort typeId))
            {
                ScaleNetManager.Logger.LogError($"Failed to get the ID of message {message.GetType()}. Skipping.");
                return;
            }
            
            // Get a pooled buffer to add the message id.
            int messageLength = bytes.Length;
            int packetLength = messageLength + 2;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(packetLength);
            
            // Add the 16-bit packet length prefix.
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, typeId);
            
            // Copy the message data to the buffer.
            bytes.CopyTo(buffer.AsSpan(2));
        
            // Send the packet.
            _client.SendAsync(buffer, 0, packetLength);
        
            // Return the buffer to the pool.
            ArrayPool<byte>.Shared.Return(buffer);
        }


#region Lifetime
        
        private void OnConnected()
        {
            ConnectionState prevState = _connectionState;
            _connectionState = ConnectionState.Connected;
            ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
        }


        private void OnDisconnected()
        {
            ConnectionState prevState = _connectionState;
            _connectionState = ConnectionState.Disconnected;
            ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
        }

#endregion


        private void OnBytesReceived(byte[] bytes, int offset, int count)
        {
            // Framing is handled automatically by SslByteMessageClient.
            
            // Ensure the message is at least 2 bytes long.
            if (count < 2)
            {
                ScaleNetManager.Logger.LogWarning("Received a message without a type ID.");
                return;
            }
            
            // Extract message type ID from the first 2 bytes.
            ushort typeId = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
            
            // Extract the message data as read-only memory.
            ReadOnlyMemory<byte> memory = new(bytes, offset + 2, count - 2);
            
            if (!NetMessages.TryDeserialize(typeId, memory, out DeserializedNetMessage message))
                return;
            
            MessageReceived?.Invoke(message);
        }


        public void Dispose()
        {
            _client.Dispose();
        }
    }
}