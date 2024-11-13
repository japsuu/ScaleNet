using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using Shared.Networking;
using Shared.Utils;
using TcpClient = NetCoreServer.TcpClient;

namespace Client.Networking.LowLevel.Transport;

public class TcpNetClientTransport(string address, int port, IPacketMiddleware? middleware = null) : TcpClient(address, port), INetClientTransport
{
    // Buffer for accumulating incomplete packet data
    private readonly MemoryStream _receiveBuffer = new();
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    
    public event Action<ConnectionStateArgs>? ConnectionStateChanged;
    public event Action<Packet>? PacketReceived;


    void INetClientTransport.Connect()
    {
        base.Connect();
    }


    void INetClientTransport.Reconnect()
    {
        base.Reconnect();
    }


    void INetClientTransport.Disconnect()
    {
        base.Disconnect();
    }


    void INetClientTransport.SendAsync(ReadOnlyMemory<byte> buffer)
    {
        middleware?.HandleOutgoingPacket(ref buffer);
        
        // Get a pooled buffer, and add the 16-bit packet length prefix.
        int packetLength = buffer.Length + 2;
        byte[] data = ArrayPool<byte>.Shared.Rent(packetLength);
        BinaryPrimitives.WriteUInt16BigEndian(data, (ushort)buffer.Length);
        buffer.Span.CopyTo(data.AsSpan(2));
        
        base.SendAsync(data, 0, packetLength);
        
        // Return the buffer to the pool.
        ArrayPool<byte>.Shared.Return(data);
    }


    void INetClientTransport.IterateIncomingPackets()
    {
        ReceiveAsync();
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
        
        while (true)
        {
            // Check if we have at least 2 bytes for the length prefix
            if (_receiveBuffer.Length < 2)
                break;

            // Set the position to the start to read the length prefix
            _receiveBuffer.Position = 0;
            byte[] lengthPrefix = new byte[2];
            _receiveBuffer.Read(lengthPrefix, 0, 2);

            // Determine the length of the packet
            ushort packetLength = BitConverter.ToUInt16(lengthPrefix, 0);

            // Check if the entire packet is in the buffer
            if (_receiveBuffer.Length < packetLength + 2)
            {
                // Reset the position to the end for appending more data later
                _receiveBuffer.Position = _receiveBuffer.Length;
                break;
            }

            // Extract the packet data (excluding the length prefix)
            byte[] packetData = ArrayPool<byte>.Shared.Rent(packetLength);
            _receiveBuffer.Read(packetData, 0, packetLength);

            // Create a packet and enqueue it
            OnReceiveFullPacket(packetData);
            
            // Return the buffer to the pool
            ArrayPool<byte>.Shared.Return(packetData);

            // Create a new MemoryStream for leftover data
            int leftoverData = (int)(_receiveBuffer.Length - _receiveBuffer.Position);
            if (leftoverData > 0)
            {
                byte[] remainingBytes = ArrayPool<byte>.Shared.Rent(leftoverData);
                
                _receiveBuffer.Read(remainingBytes, 0, leftoverData);
                _receiveBuffer.SetLength(0);
                _receiveBuffer.Write(remainingBytes, 0, remainingBytes.Length);
                
                ArrayPool<byte>.Shared.Return(remainingBytes);
            }
            else
            {
                _receiveBuffer.SetLength(0); // Clear the buffer if no data is left
            }
        }
    }


    private void OnReceiveFullPacket(byte[] data)
    {
        middleware?.HandleIncomingPacket(ref data);
        Packet packet = new(data);
        PacketReceived?.Invoke(packet);
    }


    protected override void OnError(SocketError error)
    {
        Logger.LogError($"TCP transport caught an error with code {error}");
    }
}