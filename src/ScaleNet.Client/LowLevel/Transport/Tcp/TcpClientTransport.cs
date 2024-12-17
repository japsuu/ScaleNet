using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using ScaleNet.Common;
using ScaleNet.Common.LowLevel;

namespace ScaleNet.Client.LowLevel.Transport.Tcp
{
    public sealed class TcpClientTransport : SslClient, IClientTransport
    {
        // Buffer for accumulating incomplete packet data
        private readonly MemoryStream _receiveBuffer = new();
        private readonly IPacketMiddleware? _middleware;
        private ConnectionState _connectionState = ConnectionState.Disconnected;
    
        public event Action<ConnectionStateArgs>? ConnectionStateChanged;
        public event Action<DeserializedNetMessage>? MessageReceived;


        public TcpClientTransport(ClientSslContext context, string address, int port, IPacketMiddleware? middleware = null) : base(context, address, port)
        {
            _middleware = middleware;
        }


        public void ConnectClient()
        {
            ConnectAsync();
        }


        public void ReconnectClient()
        {
            ReconnectAsync();
        }


        public void DisconnectClient()
        {
            DisconnectAsync();
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
            
            _middleware?.HandleOutgoingPacket(ref bytes);
            
            // Get a pooled buffer to add the length prefix and message id.
            int messageLength = bytes.Length;
            int packetLength = messageLength + 4;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(packetLength);
            
            // Add the 16-bit packet length prefix.
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, (ushort)messageLength);
            
            // Add the 16-bit message type ID.
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(2), typeId);
            
            // Copy the message data to the buffer.
            bytes.CopyTo(buffer.AsSpan(4));
        
            base.SendAsync(buffer, 0, packetLength);
        
            // Return the buffer to the pool.
            ArrayPool<byte>.Shared.Return(buffer);
        }


#region Lifetime

        protected override void OnConnecting() => OnConnectionStateChanged(ConnectionState.Connecting);
        protected override void OnConnected() => OnConnectionStateChanged(ConnectionState.Connected);
        protected override void OnDisconnecting() => OnConnectionStateChanged(ConnectionState.Disconnecting);
        protected override void OnDisconnected() => OnConnectionStateChanged(ConnectionState.Disconnected);


        private void OnConnectionStateChanged(ConnectionState newState)
        {
            ConnectionState prevState = _connectionState;
            _connectionState = newState;
            try
            {
                ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
            }
            catch (Exception e)
            {
                ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(ConnectionStateChanged)} event:\n{e}");
                throw;
            }
        }

#endregion


        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            // Append the received bytes to the buffer
            _receiveBuffer.Write(buffer, (int)offset, (int)size);
            _receiveBuffer.Position = 0;

            while (true)
            {
                // Check if we have at least 4 bytes for the length and type prefix
                if (_receiveBuffer.Length - _receiveBuffer.Position < 4)
                    break;

                // Read the length and type prefix
                byte[] header = new byte[4];
                int rCount = _receiveBuffer.Read(header, 0, 4);

                if (rCount != 4)
                {
                    ScaleNetManager.Logger.LogWarning("Failed to read the packet header.");
                    break;
                }

                // Interpret the length using little-endian
                ushort packetLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
                if (packetLength <= 0)
                    ScaleNetManager.Logger.LogWarning("Received a packet with a length of 0.");

                // Check if the entire packet is in the buffer
                if (_receiveBuffer.Length - _receiveBuffer.Position < packetLength)
                {
                    // Not enough data, rewind to just after the last full read for appending more data later
                    _receiveBuffer.Position -= 4; // Rewind to the start of the header
                    break;
                }

                // Extract the packet data (excluding the header)
                byte[] packetData = new byte[packetLength];
                rCount = _receiveBuffer.Read(packetData, 0, packetLength);

                if (rCount != packetLength)
                {
                    ScaleNetManager.Logger.LogWarning("Failed to read the full packet data.");
                    break;
                }

                // Interpret the type using little-endian
                ushort typeId = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2));

                // Create a packet and enqueue it
                OnReceiveFullPacket(typeId, packetData);

                // Position is naturally incremented, no manual reset required here
            }

            // Handle leftover data and re-adjust the buffer
            int leftoverData = (int)(_receiveBuffer.Length - _receiveBuffer.Position);
            if (leftoverData > 0)
            {
                byte[] remainingBytes = ArrayPool<byte>.Shared.Rent(leftoverData);

                int rCount = _receiveBuffer.Read(remainingBytes, 0, leftoverData);

                if (rCount != leftoverData)
                {
                    ScaleNetManager.Logger.LogWarning("Failed to read the leftover data.");
                    ArrayPool<byte>.Shared.Return(remainingBytes);
                    return;
                }

                _receiveBuffer.SetLength(0);
                _receiveBuffer.Write(remainingBytes, 0, leftoverData);

                ArrayPool<byte>.Shared.Return(remainingBytes);
            }
            else
                _receiveBuffer.SetLength(0); // Clear the buffer if no data is left
        }


        private void OnReceiveFullPacket(ushort typeId, byte[] data)
        {
            /*Console.WriteLine("receive:");
            Console.WriteLine(data.AsStringBits());
            Console.WriteLine(MessagePack.MessagePackSerializer.ConvertToJson(data));*/

            _middleware?.HandleIncomingPacket(ref data);
            
            if (!NetMessages.TryDeserialize(typeId, data, out DeserializedNetMessage message))
            {
                ScaleNetManager.Logger.LogError($"Failed to deserialize message with ID {typeId}.");
                return;
            }

            try
            {
                MessageReceived?.Invoke(message);
            }
            catch (Exception e)
            {
                ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(MessageReceived)} event:\n{e}");
                throw;
            }
        }


        protected override void OnError(SocketError error)
        {
            ScaleNetManager.Logger.LogError($"TCP transport caught an error with code {error}");
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _receiveBuffer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}