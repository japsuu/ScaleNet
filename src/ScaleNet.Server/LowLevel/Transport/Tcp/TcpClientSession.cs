using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using ScaleNet.Common;
using ScaleNet.Common.LowLevel;

namespace ScaleNet.Server.LowLevel.Transport.Tcp;

internal class TcpClientSession(ConnectionId id, TcpServerTransport transport, Action<SessionStateChangeArgs>? sessionStateChanged) : SslSession(transport)
{
    // Buffer for accumulating incomplete packet data
    private readonly MemoryStream _receiveBuffer = new();

    // Packets need to be stored per-session to, for example, allow sending all queued packets before disconnecting.
    public readonly ConcurrentQueue<NetMessagePacket> OutgoingPackets = new();
    public readonly ConcurrentQueue<NetMessagePacket> IncomingPackets = new();
    public readonly ConnectionId ConnectionId = id;
    
    public ConnectionState ConnectionState { get; private set; }

    
    protected override void Dispose(bool disposingManagedResources)
    {
        base.Dispose(disposingManagedResources);

        if (!disposingManagedResources)
            return;
        
        _receiveBuffer.Dispose();
            
        while (OutgoingPackets.TryDequeue(out NetMessagePacket packet))
            packet.Dispose();
            
        while (IncomingPackets.TryDequeue(out NetMessagePacket packet))
            packet.Dispose();
            
        ConnectionState = ConnectionState.Disconnected;
    }


    protected override void OnReceived(byte[] buffer, int offset, int size)
    {
        // Append the received bytes to the buffer
        _receiveBuffer.Write(buffer, offset, size);
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
        if (IncomingPackets.Count > ServerConstants.MAX_PACKETS_PER_TICK)
        {
            ScaleNetManager.Logger.LogWarning($"Session {ConnectionId} is sending too many packets. Kicking immediately.");
            transport.DisconnectSession(this, InternalDisconnectReason.TooManyPackets);
            return;
        }
        
        if (data.Length > SharedConstants.MAX_MESSAGE_SIZE_BYTES)
        {
            ScaleNetManager.Logger.LogWarning($"Session {ConnectionId} sent a packet that is too large. Kicking immediately.");
            transport.DisconnectSession(this, InternalDisconnectReason.OversizedPacket);
            return;
        }
        
        NetMessagePacket packet = NetMessagePacket.CreateIncoming(data, 0, length);
        
        transport.Middleware?.HandleIncomingPacket(ref packet);
        
        IncomingPackets.Enqueue(packet);
    }


    protected override void OnConnecting()
    {
    }


    protected override void OnConnected()
    {
    }


    protected override void OnHandshaking()
    {
    }


    protected override void OnHandshaked()
    {
        ConnectionState = ConnectionState.Connected;
        OnSessionStateChanged();
    }


    protected override void OnDisconnecting()
    {
    }


    protected override void OnDisconnected()
    {
        ConnectionState = ConnectionState.Disconnected;
        OnSessionStateChanged();
        
        transport.ReleaseSession(ConnectionId);
    }


    private void OnSessionStateChanged()
    {
        try
        {
            sessionStateChanged?.Invoke(new SessionStateChangeArgs(ConnectionId, ConnectionState));
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