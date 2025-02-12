using System.Net.Security;
using System.Net.Sockets;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

internal class ServerSslHelper(ServerSslContext? sslContext)
{
    internal bool TryCreateStream(Common.Connection conn)
    {
        NetworkStream stream = conn.Client!.GetStream();
        if (sslContext == null)
        {
            conn.Stream = stream;
            return true;
        }
        
        try
        {
            conn.Stream = CreateSslStream(stream, sslContext);
            return true;
        }
        catch (Exception e)
        {
            SimpleWebLog.Warn($"Create SSLStream Failed for connId {conn.ConnId}: {e}");
            return false;
        }
    }


    private static SslStream CreateSslStream(NetworkStream stream, ServerSslContext sslContext)
    {
        SslStream sslStream = sslContext.CertificateValidationCallback != null
            ? new SslStream(stream, true, sslContext.CertificateValidationCallback)
            : new SslStream(stream, true);
        
        //TODO: Make Async with BeginAuthenticateAsServer and callback
        sslStream.AuthenticateAsServer(sslContext.Certificate, sslContext.ClientCertificateRequired, ServerSslContext.Protocols, false);

        return sslStream;
    }
}