using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ScaleNet.Common.Ssl
{
    public class SslContext
    {
        public readonly X509Certificate Certificate;
        public readonly RemoteCertificateValidationCallback? CertificateValidationCallback;
        
        
        public SslContext(X509Certificate certificate, RemoteCertificateValidationCallback? certificateValidationCallback = null)
        {
            Certificate = certificate;
            CertificateValidationCallback = certificateValidationCallback;
        }
    }
}