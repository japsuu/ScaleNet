using Client.Networking;
using Client.Networking.LowLevel.Transport;
using Shared.Networking.Messages;
using Shared.Utils;

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
            _netClient.Update();
            
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

            //_netClient.SendMessageToServer(new ChatMessage(line));

            for (int i = 0; i < 20; i++)
            {
                _netClient.SendMessageToServer(new ChatMessage($"{line} {i}"));
            }
        }
        
        _netClient.Disconnect();
    }
}