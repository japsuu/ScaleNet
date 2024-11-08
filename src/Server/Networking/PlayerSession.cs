using System.Collections.Concurrent;
using System.Diagnostics;
using Server.Networking.LowLevel;
using Shared;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Server.Networking;

public class PlayerData(string username)
{
    public readonly string Username = username;
}

public class AuthenticationData(string personalId)
{
    public readonly string PersonalId = personalId;
}

internal class PlayerSession
{
    private readonly GameServer _server;
    private readonly ClientConnection _connection;
    private readonly ConcurrentQueue<Packet> _incomingPackets = new();
    private readonly ConcurrentQueue<Packet> _outgoingPackets = new();

    public readonly SessionId Id;
    
    public bool IsDisconnecting { get; private set; }
    
    public bool IsAuthenticated { get; private set; }

    public AuthenticationData? AuthData { get; private set; }
    
    public PlayerData? PlayerData { get; private set; }

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
        
        Logger.LogDebug($"Created session {Id} for client {ConnectionId}.");
    }


    public void LoadPlayerData()
    {
        Debug.Assert(!IsDisconnecting, "Cannot load player data for a disconnecting client.");

        if (AuthData == null)
        {
            Logger.LogError("Cannot load player data without authentication data.");
            return;
        }
        
        //TODO: Load user data from the database based on the personal ID.

        PlayerData = new PlayerData(AuthData.PersonalId);
    }


    public void SetAuthenticated(string personalId)
    {
        Debug.Assert(!IsDisconnecting, "Cannot authenticate a disconnecting client.");

        IsAuthenticated = true;
        AuthData = new AuthenticationData(personalId);
    }
    
    
    public void IterateIncoming()
    {
        Debug.Assert(!IsDisconnecting, "Cannot iterate incoming packets for a disconnecting client.");

        while (_incomingPackets.TryDequeue(out Packet packet) && !IsDisconnecting)
        {
            _server.OnPacketReceived(this, packet);
        }
    }
    
    
    public void IterateOutgoing()
    {
        while (_outgoingPackets.TryDequeue(out Packet packet) && !IsDisconnecting)
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
        Debug.Assert(!IsDisconnecting, "Cannot disconnect a client that is already disconnecting.");

        Logger.LogDebug($"Disconnecting client {Id} with reason {reason}.");
        
        if (iterateOutgoing)
        {
            // Queue a disconnect message.
            QueueSend(new DisconnectMessage(reason));
            
            IterateOutgoing();
        }
        
        _connection.Disconnect();
        
        IsDisconnecting = true;
    }


    public void QueueSend<T>(T message) where T : INetMessage
    {
        Debug.Assert(!IsDisconnecting, "Cannot send messages to a disconnecting client.");
        
        // Write to buffer.
        byte[] bytes = NetMessages.Serialize(message);
        
        Logger.LogDebug($"Queue message {message} to client.");
        
        // Enqueue the packet.
        _outgoingPackets.Enqueue(new Packet(bytes, 0, bytes.Length));
    }
    
    
    private void SendPacket(Packet packet)
    {
        _connection.SendAsync(packet.Data);
        
        Logger.LogDebug($"Sent packet to client {Id}.");
    }


    private void OnPacketReceived(Packet p) => _incomingPackets.Enqueue(p);
}