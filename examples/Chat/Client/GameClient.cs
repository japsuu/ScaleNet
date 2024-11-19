using System;
using ScaleNet.Client;
using ScaleNet.Client.LowLevel.Transport.Tcp;
using ScaleNet.Utils;
using Shared;

namespace Client;

internal class GameClient
{
    private readonly ILogger _logger;
    private readonly NetClient _netClient;


    public GameClient(string address, int port, ILogger logger)
    {
        _logger = logger;
        _netClient = new NetClient(_logger, new TcpClientTransport(address, port, _logger));
        
        _netClient.RegisterMessageHandler<ChatMessageNotification>(msg => _logger.LogInfo($"[Chat] {msg.User}: {msg.Message}"));
    }


    public void Run()
    {
        _netClient.Connect();

        _logger.LogInfo("'!' to exit");

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