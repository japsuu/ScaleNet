using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using ScaleNet.Common;

namespace ScaleNet.Server.LowLevel.Transport.Tcp;

internal class TcpClientSession(SessionId id, TcpServerTransport transport, Action<SessionStateChangeArgs>? sessionStateChanged) : SslSession(transport)
{
    // Buffer for accumulating incomplete packet data
    private readonly MemoryStream _receiveBuffer = new();

    // Packets need to be stored per-session to, for example, allow sending all queued packets before disconnecting.
    public readonly ConcurrentQueue<TcpServerTransport.Packet> OutgoingPackets = new();
    public readonly ConcurrentQueue<TcpServerTransport.Packet> IncomingPackets = new();
    public readonly SessionId SessionId = id;
    
    public ConnectionState ConnectionState { get; private set; }


    protected override void OnReceived(byte[] buffer, int offset, int size)
    {
        // Append the received bytes to the buffer
        _receiveBuffer.Write(buffer, offset, size);
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
        if (IncomingPackets.Count > ServerConstants.MAX_PACKETS_PER_TICK)
        {
            ScaleNetManager.Logger.LogWarning($"Session {SessionId} is sending too many packets. Kicking immediately.");
            transport.DisconnectSession(this, InternalDisconnectReason.TooManyPackets);
            return;
        }
        
        if (data.Length > SharedConstants.MAX_PACKET_SIZE_BYTES)
        {
            ScaleNetManager.Logger.LogWarning($"Session {SessionId} sent a packet that is too large. Kicking immediately.");
            transport.DisconnectSession(this, InternalDisconnectReason.OversizedPacket);
            return;
        }
        
        transport.Middleware?.HandleIncomingPacket(ref data);
        
        TcpServerTransport.Packet packet = new(typeId, data);
        IncomingPackets.Enqueue(packet);
    }


    protected override void OnConnecting()
    {
        ConnectionState = ConnectionState.Connecting;
        OnSessionStateChanged();
    }


    protected override void OnConnected()
    {
        ConnectionState = ConnectionState.Connected;
        OnSessionStateChanged();
    }


    protected override void OnHandshaking()
    {
        ConnectionState = ConnectionState.SslHandshaking;
        OnSessionStateChanged();
    }


    protected override void OnHandshaked()
    {
        ConnectionState = ConnectionState.Ready;
        OnSessionStateChanged();
    }


    protected override void OnDisconnecting()
    {
        ConnectionState = ConnectionState.Disconnecting;
        OnSessionStateChanged();
    }


    protected override void OnDisconnected()
    {
        ConnectionState = ConnectionState.Disconnected;
        OnSessionStateChanged();
        
        transport.ReleaseSession(SessionId);
    }


    private void OnSessionStateChanged()
    {
        try
        {
            sessionStateChanged?.Invoke(new SessionStateChangeArgs(SessionId, ConnectionState));
        }
        catch (Exception e)
        {
            ScaleNetManager.Logger.LogError($"User code threw an exception in the {nameof(sessionStateChanged)} event:\n{e}");
            throw;
        }
    }


    protected override void OnError(SocketError error)
    {
        ScaleNetManager.Logger.LogError($"TCP session with Id {Id} caught an error: {error}");
    }
}