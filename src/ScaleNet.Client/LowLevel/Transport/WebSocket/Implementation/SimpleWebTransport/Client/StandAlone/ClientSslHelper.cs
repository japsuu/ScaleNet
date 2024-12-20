using System;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Client.StandAlone
{
    internal class ClientSslHelper
    {
        private readonly ClientSslContext? _sslContext;


        public ClientSslHelper(ClientSslContext? sslContext)
        {
            _sslContext = sslContext;
        }


        internal bool TryCreateStream(Connection conn, Uri uri)
        {
            NetworkStream stream = conn.Client!.GetStream();
            if (uri.Scheme != "wss")
            {
                conn.Stream = stream;
                return true;
            }

            Debug.Assert(_sslContext != null, "_sslContext != null");

            try
            {
                conn.Stream = CreateSslStream(stream, uri, _sslContext);
                return true;
            }
            catch (Exception e)
            {
                SimpleWebLog.Error($"Create SSLStream Failed: {e}", false);
                return false;
            }
        }


        private static SslStream CreateSslStream(NetworkStream stream, Uri uri, ClientSslContext sslContext)
        {
            SslStream sslStream = new(stream, true, sslContext.CertificateValidationCallback);

            if (sslContext.Certificates != null)
                sslStream.AuthenticateAsClient(uri.Host, sslContext.Certificates, ClientSslContext.Protocols, false);
            else
                sslStream.AuthenticateAsClient(uri.Host);

            return sslStream;
        }
    }
}