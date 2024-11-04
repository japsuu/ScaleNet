using Shared.Networking.Messages;

namespace Shared.Networking;

public abstract class NetManager
{
    public NetManager()
    {
        MessageManager.RegisterAllMessages();
    }
}