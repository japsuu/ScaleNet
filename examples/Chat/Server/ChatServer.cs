using System.Diagnostics;
using System.Net;
using ScaleNet.Common;
using ScaleNet.Server;
using ScaleNet.Server.LowLevel;
using ScaleNet.Server.LowLevel.Transport;
using ScaleNet.Server.LowLevel.Transport.Tcp;
using Server.Authentication;
using Server.Database;
using Shared;
using Shared.Authentication;
using NetMessages = Shared.NetMessages;

namespace Server;

internal class BasicConnectionManager(IServerTransport transport) : ConnectionManager<ClientConnection>(transport)
{
    protected override ClientConnection CreateConnection(ConnectionId connectionId, IServerTransport transport) => new(connectionId, transport);
}

internal sealed class ChatServer : IDisposable
{
    private readonly ServerNetworkManager<ClientConnection> _netManager;
    private readonly Authenticator _authenticator;
    private readonly IDatabaseAccess _databaseAccess;


    public ChatServer(ServerSslContext context, IPAddress address, int port, int maxConnections, bool allowAccountRegistration)
    {
        ScaleNetManager.Initialize();

        InMemoryDatabase db = new();
        _databaseAccess = db;

        TcpServerTransport transport = new(context, address, port, maxConnections);
        BasicConnectionManager connectionManager = new(transport);
        _netManager = new ServerNetworkManager<ClientConnection>(transport, connectionManager);

        _authenticator = new Authenticator(_netManager, _databaseAccess, allowAccountRegistration);
        _authenticator.ClientAuthSuccess += OnClientAuthenticated;

        _netManager.ClientStateChanged += OnClientStateChanged;

        _netManager.RegisterMessageHandler<NetMessages.ChatMessage>(OnChatMessageReceived);
    }


    public void Run()
    {
        _netManager.Start();

        ScaleNetManager.Logger.LogInfo("Server started.");

        while (_netManager.IsStarted)
        {
            _netManager.Update();

            Thread.Sleep(1000 / ServerConstants.TICKS_PER_SECOND);
        }

        _netManager.Stop();
    }


    private void OnClientStateChanged(ClientStateChangeArgs<ClientConnection> args)
    {
        switch (args.NewState)
        {
            case ConnectionState.Connected:
            {
                _authenticator.OnNewClientReadyForAuthentication(args.Connection);
                break;
            }
            case ConnectionState.Disconnected:
            {
                ClientConnection connection = args.Connection;

                // Only authenticated sessions have player data.
                if (connection.IsAuthenticated)
                    _netManager.SendMessageToAllClientsExcept(
                        new NetMessages.ChatMessageNotification(connection.PlayerData!.Username, "Left the chat."), connection);
                break;
            }
        }
    }


    private void OnChatMessageReceived(ClientConnection connection, NetMessages.ChatMessage msg)
    {
        if (!connection.IsAuthenticated)
        {
            connection.Kick(DisconnectReason.ExploitAttempt);
            return;
        }

        ScaleNetManager.Logger.LogInfo($"Received chat message from {connection.ConnectionId}: {msg.Message}");

        // If the message is empty, ignore it.
        if (string.IsNullOrWhiteSpace(msg.Message))
            return;

        // Forward the message to all clients.
        _netManager.SendMessageToAllClients(new NetMessages.ChatMessageNotification(connection.PlayerData!.Username, msg.Message));
    }


    public void Dispose()
    {
        _netManager.Dispose();
    }


#region Authentication

    /// <summary>
    /// Called when a remote client authenticates with the server.
    /// </summary>
    private void OnClientAuthenticated(ClientConnection connection)
    {
        Debug.Assert(connection.IsAuthenticated, "Client is not authenticated.");

        AccountUID accountId = connection.AccountId;
        ScaleNetManager.Logger.LogInfo($"Session {connection.ConnectionId} authenticated as account {accountId}.");

        // Load user data.
        if (!_databaseAccess.TryGetPlayerData(accountId, out PlayerData? playerData))
        {
            ScaleNetManager.Logger.LogWarning($"Session {connection.ConnectionId} player data could not be loaded.");
            connection.Kick(DisconnectReason.CorruptPlayerData);
            return;
        }

        connection.PlayerData = playerData;

        _netManager.SendMessageToAllClientsExcept(new NetMessages.ChatMessageNotification(connection.PlayerData!.Username, "Joined the chat."), connection);
    }

#endregion
}