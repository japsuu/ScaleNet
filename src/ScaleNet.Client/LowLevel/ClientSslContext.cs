using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace ScaleNet.Client.LowLevel
{
    /// <summary>
    /// SSL context
    /// </summary>
    public class ClientSslContext
    {
        /// <summary>
        /// Initialize SSL context with given certificate and validation callback
        /// </summary>
        /// <param name="certificate">SSL certificate</param>
        /// <param name="certificateValidationCallback">SSL certificate</param>
        public ClientSslContext(X509Certificate2 certificate, RemoteCertificateValidationCallback? certificateValidationCallback = null)
        {
            Certificates = new X509Certificate2Collection(new[] {certificate});
            CertificateValidationCallback = certificateValidationCallback;
        }


        /// <summary>
        /// Initialize SSL context with given certificates collection and validation callback
        /// </summary>
        /// <param name="certificates">SSL certificates collection</param>
        /// <param name="certificateValidationCallback">SSL certificate</param>
        public ClientSslContext(X509Certificate2Collection certificates, RemoteCertificateValidationCallback? certificateValidationCallback = null)
        {
            Certificates = certificates;
            CertificateValidationCallback = certificateValidationCallback;
        }


        /// <summary>
        /// SSL protocols
        /// </summary>
        public static SslProtocols Protocols => SslProtocols.Tls12;

        /// <summary>
        /// SSL certificates collection
        /// </summary>
        public X509Certificate2Collection? Certificates { get; set; }

        /// <summary>
        /// SSL certificate validation callback
        /// </summary>
        public RemoteCertificateValidationCallback? CertificateValidationCallback { get; set; }
    }
}