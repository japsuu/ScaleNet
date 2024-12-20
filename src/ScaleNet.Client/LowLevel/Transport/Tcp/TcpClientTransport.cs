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

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public event Action<ConnectionStateArgs>? ConnectionStateChanged;
        public event Action<DeserializedNetMessage>? MessageReceived;


        public TcpClientTransport(ClientSslContext context, string address, ushort port, IPacketMiddleware? middleware = null) : base(context, address, port)
        {
            _middleware = middleware;
        }


        public bool ConnectClient()
        {
            return ConnectAsync();
        }


        public bool ReconnectClient()
        {
            return ReconnectAsync();
        }


        public bool DisconnectClient()
        {
            return DisconnectAsync();
        }


        public void IterateIncoming()
        {
            //TODO: Iterate only when called.
        }


        public void IterateOutgoing()
        {
            //TODO: Iterate only when called.
        }


        public void SendAsync<T>(T message) where T : INetMessage
        {
            // Write to a packet.
            if (!NetMessages.TrySerialize(message, out NetMessagePacket packet))
                return;
        
            if (packet.Length > SharedConstants.MAX_MESSAGE_SIZE_BYTES)
            {
                ScaleNetManager.Logger.LogError($"Message {message} exceeds maximum msg size of {SharedConstants.MAX_MESSAGE_SIZE_BYTES} bytes. Skipping.");
                return;
            }
            
            _middleware?.HandleOutgoingPacket(ref packet);
            
            // Get a pooled buffer to add the length prefix.
            int payloadLength = packet.Length;
            int packetLength = payloadLength + 2;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(packetLength);
            
            // Add the 16-bit packet length prefix.
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, (ushort)payloadLength);
            
            // Copy the message data to the buffer.
            packet.AsSpan().CopyTo(buffer.AsSpan(2));

            base.SendAsync(buffer, 0, packetLength);
        
            // Return the buffer to the pool.
            ArrayPool<byte>.Shared.Return(buffer);
            
            packet.Dispose();
        }


#region Lifetime

        protected override void OnConnecting() => OnConnectionStateChanged(ConnectionState.Connecting);
        protected override void OnConnected() => OnConnectionStateChanged(ConnectionState.Connected);
        protected override void OnDisconnecting() => OnConnectionStateChanged(ConnectionState.Disconnecting);
        protected override void OnDisconnected() => OnConnectionStateChanged(ConnectionState.Disconnected);


        private void OnConnectionStateChanged(ConnectionState newState)
        {
            State = newState;
            try
            {
                ConnectionStateChanged?.Invoke(new ConnectionStateArgs(State));
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
                // Check if we have at least 2 bytes for the length prefix
                if (_receiveBuffer.Length - _receiveBuffer.Position < 2)
                    break;

                // Read the length prefix
                byte[] header = new byte[2];
                int rCount = _receiveBuffer.Read(header, 0, 2);
        
                if (rCount != 2)
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
                    _receiveBuffer.Position -= 2; // Rewind to the start of the header
                    break;
                }

                // Extract the packet data (excluding the header)
                byte[] packetData = ArrayPool<byte>.Shared.Rent(packetLength);
                rCount = _receiveBuffer.Read(packetData, 0, packetLength);
        
                if (rCount != packetLength)
                {
                    ScaleNetManager.Logger.LogWarning("Failed to read the full packet data.");
                    break;
                }
            
                // Create a packet and enqueue it
                OnReceiveFullPacket(packetData, packetLength);

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


        private void OnReceiveFullPacket(byte[] data, int length)
        {
            NetMessagePacket packet = NetMessagePacket.CreateIncomingNoCopy(data, 0, length, false);
        
            _middleware?.HandleIncomingPacket(ref packet);
            
            bool serializeSuccess = NetMessages.TryDeserialize(packet, out DeserializedNetMessage msg);
                
            if (!serializeSuccess)
            {
                ScaleNetManager.Logger.LogWarning("Received a packet that could not be deserialized.");
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