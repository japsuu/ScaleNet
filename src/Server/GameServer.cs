using System.Net;
using Server.Networking;
using Server.Networking.Authentication.Resolvers;
using Server.Networking.Database;
using Server.Networking.LowLevel.Transport.Tcp;
using Shared;
using Shared.Networking;
using Shared.Utils;

namespace Server;

internal class GameServer
{
    private readonly NetServer _netServer;


    public GameServer(IPAddress address, int port, int maxConnections, bool allowAccountRegistration)
    {
        InMemoryDatabase db = new();
        _netServer = new NetServer(
            new TcpServerTransport(address, port, maxConnections),
            new DatabaseAuthenticationResolver(db),
            db,
            allowAccountRegistration);
        
        _netServer.ClientStateChanged += OnClientStateChanged;
        _netServer.ClientAuthenticated += client => _netServer.SendMessageToAllClientsExcept(new ChatMessageNotification(client.PlayerData!.Username, "Joined the chat."), client);;
        
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


    private void OnClientStateChanged(ClientStateChangeArgs args)
    {
        if (args.NewState != ConnectionState.Disconnected)
            return;
        
        Client client = args.Client;
        
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