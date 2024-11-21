using System;
using ScaleNet.Client;
using ScaleNet.Client.LowLevel.Transport.Tcp;
using ScaleNet.Common;
using ScaleNet.Common.Ssl;
using ScaleNet.Common.Utils;
using Shared;

namespace Client;

internal class GameClient
{
    private readonly NetClient _netClient;


    public GameClient(SslContext context, string address, int port)
    {
        (string username, string password) = GetCredentials();

        Networking.Initialize();
        
        _netClient = new NetClient(new TcpClientTransport(context, address, port));
        
        _netClient.ReceivedAuthInfo += () => _netClient.RequestRegister(username, password);
        _netClient.AccountCreationResultReceived += result =>
        {
            if (result == AccountCreationResult.Success)
            {
                Networking.Logger.LogInfo("Account created successfully");
                _netClient.RequestLogin(username, password);
            }
            else
                Networking.Logger.LogError("Failed to create account");
        };
        _netClient.AuthenticationResultReceived += result =>
        {
            if (result == AuthenticationResult.Success)
                Networking.Logger.LogInfo("Authenticated successfully");
            else
                Networking.Logger.LogError("Failed to authenticate");
        };
        
        _netClient.RegisterMessageHandler<ChatMessageNotification>(msg => Networking.Logger.LogInfo($"[Chat] {msg.User}: {msg.Message}"));
    }


    public void Run()
    {
        _netClient.Connect();

        Networking.Logger.LogInfo("'!' to exit");

        while (_netClient.IsConnected)
        {
            if (!_netClient.IsAuthenticated)
                continue;
            
            string? line = Console.ReadLine();
            
            // Since Console.ReadLine is blocking, we need to check if the client is still connected and authenticated
            if (!_netClient.IsConnected || !_netClient.IsAuthenticated)
                break;
            
            if (string.IsNullOrWhiteSpace(line))
                continue;
            
            if (line == "!")
                break;
            
            ConsoleUtils.ClearPreviousConsoleLine();

            _netClient.SendMessageToServer(new ChatMessage(line));
        }
        
        _netClient.Disconnect();
    }


    private static (string, string) GetCredentials()
    {
        Console.WriteLine("Enter your username:");
        string username = Console.ReadLine() ?? RandomUtils.RandomString(8);
        
        Console.WriteLine("Enter your password:");
        string password = Console.ReadLine() ?? RandomUtils.RandomString(8);
        
        return (username, password);
    }
}