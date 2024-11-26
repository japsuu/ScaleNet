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
        /// Initialize SSL context with given certificate and validation callback
        /// </summary>
        /// <param name="certificate">SSL certificate</param>
        /// <param name="certificateValidationCallback">SSL certificate</param>
        public ServerSslContext(X509Certificate certificate, RemoteCertificateValidationCallback? certificateValidationCallback = null)
        {
            Certificate = certificate;
            CertificateValidationCallback = certificateValidationCallback;
        }


        /// <summary>
        /// SSL protocols
        /// </summary>
        public static SslProtocols Protocols => SslProtocols.Tls12;

        /// <summary>
        /// SSL certificate
        /// </summary>
        public X509Certificate Certificate { get; set; }

        /// <summary>
        /// SSL certificate validation callback
        /// </summary>
        public RemoteCertificateValidationCallback? CertificateValidationCallback { get; set; }

        /// <summary>
        /// If the client is asked for a certificate for authentication.
        /// Note that this is only a request - if no certificate is provided, the server still accepts the connection request.
        /// </summary>
        public bool ClientCertificateRequired { get; set; }
    }
}