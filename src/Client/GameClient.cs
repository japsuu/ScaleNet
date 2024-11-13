using Client.Networking;
using Client.Networking.LowLevel.Transport;
using Shared.Networking.Messages;
using Shared.Utils;

namespace Client;

internal class GameClient
{
    private readonly NetClient _netClient;
    
    public bool IsConnected => _netClient.IsConnected;
    public bool IsAuthenticated => _netClient.IsAuthenticated;


    public GameClient(string address, int port)
    {
        _netClient = new NetClient(new TcpNetClientTransport(address, port));
        
        _netClient.RegisterMessageHandler<ChatMessageNotification>(OnChatMessage);
    }


    private void OnChatMessage(ChatMessageNotification msg)
    {
        //Logger.LogInfo($"[Chat] {msg.User}: {msg.Message}");
    }


    public void Connect()
    {
        _netClient.Connect();
    }
    
    
    public void SendTestMessage(int num)
    {
        _netClient.SendMessageToServer(new ChatMessage($"Test {num}"));
    }
    
    
    public void Disconnect()
    {
        _netClient.Disconnect();
    }
}