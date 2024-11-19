using ScaleNet.Client;
using ScaleNet.Client.LowLevel.Transport;
using ScaleNet.Client.LowLevel.Transport.Tcp;
using ScaleNet.Networking;
using ScaleNet.Utils;
using Shared;

namespace Client;

internal class GameClient
{
    private readonly NetClient _netClient;


    public GameClient(string address, int port)
    {
        _netClient = new NetClient(new TcpNetClientTransport(address, port));
        
        _netClient.RegisterMessageHandler<ChatMessageNotification>(msg => Logger.LogInfo($"[Chat] {msg.User}: {msg.Message}"));
    }


    public void Run()
    {
        _netClient.Connect();

        Logger.LogInfo("'!' to exit");

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