using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using ScaleNet.Networking;
using ScaleNet.Utils;
using TcpClient = NetCoreServer.TcpClient;

namespace ScaleNet.Client.LowLevel.Transport.Tcp
{
    public class TcpNetClientTransport : TcpClient, INetClientTransport
    {
        // Buffer for accumulating incomplete packet data
        private readonly MemoryStream _receiveBuffer = new();
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private readonly IPacketMiddleware? _middleware;
    
        public event Action<ConnectionStateArgs>? ConnectionStateChanged;
        public event Action<Packet>? PacketReceived;


        public TcpNetClientTransport(string address, int port, IPacketMiddleware? middleware = null) : base(address, port)
        {
            _middleware = middleware;
        }


        void INetClientTransport.Connect()
        {
            if (!base.Connect())
            {
                Logger.LogError("Failed to connect to the server.");
                return;
            }
        
            base.ReceiveAsync();
        }


        void INetClientTransport.Reconnect()
        {
            base.Reconnect();
        }


        void INetClientTransport.Disconnect()
        {
            base.Disconnect();
        }


        void INetClientTransport.SendAsync(Memory<byte> buffer)
        {
            _middleware?.HandleOutgoingPacket(ref buffer);
        
            // Get a pooled buffer, and add the 16-bit packet length prefix.
            int packetLength = buffer.Length + 2;
            byte[] data = ArrayPool<byte>.Shared.Rent(packetLength);
            BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)buffer.Length);
            buffer.Span.CopyTo(data.AsSpan(2));
        
            base.SendAsync(data, 0, packetLength);
        
            // Return the buffer to the pool.
            ArrayPool<byte>.Shared.Return(data);
        }


#region Lifetime

        protected override void OnConnecting()
        {
            ConnectionState prevState = _connectionState;
            _connectionState = ConnectionState.Connecting;
            ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
        }


        protected override void OnConnected()
        {
            ConnectionState prevState = _connectionState;
            _connectionState = ConnectionState.Connected;
            ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
        }

    
        protected override void OnDisconnecting()
        {
            ConnectionState prevState = _connectionState;
            _connectionState = ConnectionState.Disconnecting;
            ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
        }


        protected override void OnDisconnected()
        {
            ConnectionState prevState = _connectionState;
            _connectionState = ConnectionState.Disconnected;
            ConnectionStateChanged?.Invoke(new ConnectionStateArgs(_connectionState, prevState));
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
                byte[] lengthPrefix = new byte[2];
                int rCount = _receiveBuffer.Read(lengthPrefix, 0, 2);
            
                if (rCount != 2)
                {
                    Logger.LogWarning("Failed to read the packet length prefix.");
                    break;
                }

                // Interpret the length using little-endian
                ushort packetLength = BinaryPrimitives.ReadUInt16LittleEndian(lengthPrefix);
            
                if (packetLength <= 0)
                    Logger.LogWarning("Received a packet with a length of 0.");

                // Check if the entire packet is in the buffer
                if (_receiveBuffer.Length - _receiveBuffer.Position < packetLength)
                {
                    // Not enough data, rewind to just after the last full read for appending more data later
                    _receiveBuffer.Position -= 2; // Rewind to the start of the length prefix
                    break;
                }

                // Extract the packet data (excluding the length prefix)
                byte[] packetData = new byte[packetLength];
                rCount = _receiveBuffer.Read(packetData, 0, packetLength);
            
                if (rCount != packetLength)
                {
                    Logger.LogWarning("Failed to read the full packet data.");
                    break;
                }

                // Create a packet and enqueue it
                OnReceiveFullPacket(packetData.AsMemory());

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
                    Logger.LogWarning("Failed to read the leftover data.");
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


        private void OnReceiveFullPacket(Memory<byte> data)
        {
            /*Console.WriteLine("receive:");
            Console.WriteLine(data.AsStringBits());
            Console.WriteLine(MessagePack.MessagePackSerializer.ConvertToJson(data));*/

            _middleware?.HandleIncomingPacket(ref data);
            Packet packet = new(data);
            PacketReceived?.Invoke(packet);
        }


        protected override void OnError(SocketError error)
        {
            Logger.LogError($"TCP transport caught an error with code {error}");
        }
    }
}