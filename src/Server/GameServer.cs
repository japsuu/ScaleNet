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
        
        _netServer.ClientStateChanged += OnClientStateChanged;
        
        _netServer.RegisterMessageHandler<ChatMessage>(OnChatMessageReceived);
    }


    public void Run()
    {
        _netServer.Start();
        
        Logger.LogInfo("Server started.");
        
        while (_netServer.IsStarted)
        {
            _netServer.Update();
            
            Thread.Sleep(1000 / ServerConstants.TICKS_PER_SECOND);
        }
        
        _netServer.Stop();
    }


    private void OnClientStateChanged(ClientStateArgs obj)
    {
        Client client = obj.Client;
        
        // Only authenticated sessions have player data.
        if (client.IsAuthenticated)
            _netServer.SendMessageToAllClientsExcept(new ChatMessageNotification(client.PlayerData!.Username, "Left the chat."), client);
    }


    private void OnChatMessageReceived(Client client, ChatMessage msg)
    {
        Logger.LogInfo($"Received chat message from {client.SessionId}: {msg.Message}");
        
        // If the message is empty, ignore it.
        if (string.IsNullOrWhiteSpace(msg.Message))
            return;
        
        // Forward the message to all clients.
        _netServer.SendMessageToAllClients(new ChatMessageNotification(client.PlayerData!.Username, msg.Message));
    }
}