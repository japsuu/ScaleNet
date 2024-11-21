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
        Networking.Initialize();
        
        _netClient = new NetClient(new TcpClientTransport(context, address, port));
        
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
}