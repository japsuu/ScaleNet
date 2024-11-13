using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using NetCoreServer;
using Shared;
using Shared.Networking;
using Shared.Utils;

namespace Server.Networking.LowLevel.Transport.Tcp;

internal class TcpClientSession(SessionId id, TcpServerTransport transport) : TcpSession(transport)
{
    // Buffer for accumulating incomplete packet data
    private readonly MemoryStream _receiveBuffer = new();
    
    // Packets need to be stored per-session to, for example, allow sending all queued packets before disconnecting.
    public readonly ConcurrentQueue<TcpServerTransport.Packet> OutgoingPackets = new();
    public readonly ConcurrentQueue<TcpServerTransport.Packet> IncomingPackets = new();
    public readonly SessionId SessionId = id;


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


    private void OnReceiveFullPacket(byte[] data, int length)
    {
        /*if (IncomingPackets.Count > ServerConstants.MAX_PACKETS_PER_TICK)
        {
            Logger.LogWarning($"Session {SessionId} is sending too many packets. Kicking immediately.");
            transport.DisconnectSession(this, DisconnectReason.TooManyPackets);
            return;
        }*/
        
        /*Console.WriteLine("receive:");
        Console.WriteLine(data.AsStringBits());
        Console.WriteLine(MessagePack.MessagePackSerializer.ConvertToJson(data));*/
        
        transport.Middleware?.HandleIncomingPacket(ref data);
        
        TcpServerTransport.Packet packet = new(data, 0, length);
        IncomingPackets.Enqueue(packet);
    }


    protected override void OnError(SocketError error)
    {
        Logger.LogError($"TCP session with Id {Id} caught an error: {error}");
    }
}