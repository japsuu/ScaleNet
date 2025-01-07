using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace ScaleNet.Server.LowLevel
{
    /// <summary>
    /// Server SSL context
    /// </summary>
    public class ServerSslContext
    {
        /// <summary>
        /// Initialize SSL context with the given certificate.
        /// </summary>
        public ServerSslContext(X509Certificate certificate)
        {
            Certificate = certificate;
            CertificateValidationCallback = null;
            ClientCertificateRequired = false;
        }
        
        /// <summary>
        /// Initialize SSL context with the given certificate,
        /// and require the client to provide a certificate for authentication.
        /// </summary>
        public ServerSslContext(X509Certificate certificate, RemoteCertificateValidationCallback certificateValidationCallback)
        {
            Certificate = certificate;
            CertificateValidationCallback = certificateValidationCallback;
            ClientCertificateRequired = true;
        }


        /// <summary>
        /// SSL protocols
        /// </summary>
        public static SslProtocols Protocols => SslProtocols.Tls12;

        /// <summary>
        /// SSL certificate
        /// </summary>
        public readonly X509Certificate Certificate;

        /// <summary>
        /// SSL certificate validation callback
        /// </summary>
        public readonly RemoteCertificateValidationCallback? CertificateValidationCallback;

        /// <summary>
        /// If the client is asked for a certificate for authentication.
        /// Note that this is only a request - if no certificate is provided, the server still accepts the connection request.
        /// </summary>
        public readonly bool ClientCertificateRequired;
    }
}