using System.Collections.Concurrent;
using NetStack.Buffers;
using NetStack.Serialization;
using Server.Networking.LowLevel;
using Shared;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Server.Networking;

internal class PlayerSession
{
    private readonly GameServer _server;
    private readonly ClientConnection _connection;
    private readonly ConcurrentQueue<Packet> _incomingPackets = new();
    private readonly ConcurrentQueue<Packet> _outgoingPackets = new();

    public readonly SessionId Id;
    
    public bool IsAuthenticated { get; private set; }

    public bool RejectNewPackets
    {
        get => _connection.RejectNewPackets;
        set => _connection.RejectNewPackets = value;
    }
    
    public Guid ConnectionId => _connection.Id;


    public PlayerSession(SessionId id, GameServer server, ClientConnection connection)
    {
        Id = id;
        _server = server;
        _connection = connection;
        
        _connection.PacketReceived += OnPacketReceived;
    }


    public void SetAuthenticated()
    {
        IsAuthenticated = true;
    }
    
    
    public void IterateIncoming()
    {
        while (_incomingPackets.TryDequeue(out Packet packet))
        {
            _server.HandlePacket(this, packet);
        }
    }
    
    
    public void IterateOutgoing()
    {
        while (_outgoingPackets.TryDequeue(out Packet packet))
        {
            SendPacket(packet);
        }
    }
    
    
    /// <summary>
    /// Disconnect the client.
    /// </summary>
    /// <param name="reason">The reason for the disconnection.</param>
    /// <param name="iterateOutgoing">True to send outgoing packets before disconnecting.</param>
    ///
    /// <remarks>
    /// The disconnect reason will only be sent to the client if <paramref name="iterateOutgoing"/> is true.
    /// </remarks>
    public void Kick(DisconnectReason reason, bool iterateOutgoing = true)
    {
        Logger.LogDebug($"Disconnecting client {Id} with reason {reason}.");
        
        if (iterateOutgoing)
        {
            // Queue a disconnect message.
            QueueSend(new DisconnectMessage(reason));
            
            IterateOutgoing();
        }
        else
        {
            // Return all pooled packets.
            while (_outgoingPackets.TryDequeue(out Packet packet))
                ArrayPool<byte>.Shared.Return(packet.Data.Array!);
        }
        
        _connection.Disconnect();
    }


    public void QueueSend<T>(T message) where T : NetMessage
    {
        // Write to buffer.
        BitBuffer buffer = PacketBufferPool.GetBitBuffer();
        buffer.AddByte(MessageManager.NetMessages.GetId<T>());
        message.Serialize(buffer);
        
        Logger.LogDebug($"Queue message {message} to client.");
        
        int bufferLength = buffer.Length;
        
        // Get a pooled byte[] buffer.
        byte[] bytes = ArrayPool<byte>.Shared.Rent(bufferLength);
        
        // Enqueue the packet.
        buffer.ToArray(bytes);
        _outgoingPackets.Enqueue(new Packet(bytes, 0, bufferLength));
        
        buffer.Clear();
    }
    
    
    private void SendPacket(Packet packet)
    {
        _connection.SendAsync(packet.Data);
        
        ArrayPool<byte>.Shared.Return(packet.Data.Array!);
        
        Logger.LogDebug($"Sent packet to client {Id}.");
    }


    private void OnPacketReceived(Packet p) => _incomingPackets.Enqueue(p);
}