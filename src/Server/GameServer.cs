using System.Net;
using Server.Networking;
using Server.Networking.HighLevel;
using Server.Networking.LowLevel.Transport;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Server;

internal class GameServer
{
    private readonly NetServer _netServer;


    public GameServer(IPAddress address, int port)
    {
        _netServer = new NetServer(new TcpServerTransport(address, port, ServerConstants.MAX_CONNECTIONS));
        
        _netServer.RegisterMessageHandler<ChatMessage>(OnChatMessageReceived);
    }


    private void OnChatMessageReceived(Client client, ChatMessage msg)
    {
        Logger.LogInfo($"Received chat message from {client.Id}: {msg.Message}");
        
        // If the message is empty, ignore it.
        if (string.IsNullOrWhiteSpace(msg.Message))
            return;
        
        // Forward the message to all clients.
        _netServer.SendMessageToAllClients(new ChatMessageNotification(client.PlayerData!.Username, msg.Message));
    }
}