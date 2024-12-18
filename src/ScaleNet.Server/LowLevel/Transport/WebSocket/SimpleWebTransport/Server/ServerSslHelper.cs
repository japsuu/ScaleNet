using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

internal class ServerSslHelper(ServerSslContext? sslContext)
{
    internal bool TryCreateStream(Common.Connection conn)
    {
        NetworkStream stream = conn.client.GetStream();
        if (sslContext == null)
        {
            conn.stream = stream;
            return true;
        }
        
        try
        {
            conn.stream = CreateSslStream(stream, sslContext);
            return true;
        }
        catch (Exception e)
        {
            SimpleWebLog.Error($"Create SSLStream Failed: {e}", false);
            return false;
        }
    }


    private static SslStream CreateSslStream(NetworkStream stream, ServerSslContext sslContext)
    {
        SslStream sslStream = new(stream, true, sslContext.CertificateValidationCallback);
        sslStream.AuthenticateAsServer(sslContext.Certificate, sslContext.ClientCertificateRequired, ServerSslContext.Protocols, false);

        return sslStream;
    }
}