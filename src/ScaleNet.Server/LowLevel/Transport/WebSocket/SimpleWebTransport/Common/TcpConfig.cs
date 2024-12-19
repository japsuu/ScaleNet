using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

[Serializable]
public struct TcpConfig
{
    public readonly bool NoDelay;
    public readonly int SendTimeout;
    public readonly int ReceiveTimeout;


    public TcpConfig(bool noDelay, int sendTimeout, int receiveTimeout)
    {
        NoDelay = noDelay;
        SendTimeout = sendTimeout;
        ReceiveTimeout = receiveTimeout;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Apply(TcpConfig config, TcpClient client)
    {
        client.SendTimeout = config.SendTimeout;
        client.ReceiveTimeout = config.ReceiveTimeout;
        client.NoDelay = config.NoDelay;
    }
}