using System.Diagnostics;
using Shared.Networking;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Server.Networking.HighLevel;

public class PlayerData(string username)
{
    public readonly string Username = username;
}

public class AuthenticationData(string personalId)
{
    public readonly string PersonalId = personalId;
}

public class Client
{
    private readonly NetServer _server;

    public readonly SessionId Id;
    
    public bool IsDisconnecting { get; private set; }
    public bool IsAuthenticated { get; private set; }

    public AuthenticationData? AuthData { get; private set; }
    public PlayerData? PlayerData { get; private set; }


    public Client(SessionId id, NetServer server)
    {
        Id = id;
        _server = server;
        
    }


    public bool LoadPlayerData()
    {
        Debug.Assert(!IsDisconnecting, "Cannot load player data for a disconnecting client.");

        if (AuthData == null)
        {
            Logger.LogError("Cannot load player data without authentication data.");
            return false;
        }
        
        //TODO: Load user data from the database based on the personal ID.

        PlayerData = new PlayerData(AuthData.PersonalId);
        return true;
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


    private void OnPacketReceived(Packet p)
    {
        if (_incomingPackets.Count >= ServerConstants.MAX_PACKETS_PER_TICK)
        {
            Logger.LogWarning($"Client {Id} is sending too many packets.");
            Kick(DisconnectReason.TooManyPackets);
            return;
        }
        
        _incomingPackets.Enqueue(p);
    }
}