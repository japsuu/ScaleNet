using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ScaleNet.Common.Ssl
{
    public class SslContext
    {
        public readonly X509Certificate2 Certificate;
        public readonly RemoteCertificateValidationCallback CertificateValidationCallback;
        
        
        public SslContext(X509Certificate2 certificate, RemoteCertificateValidationCallback certificateValidationCallback)
        {
            Certificate = certificate;
            CertificateValidationCallback = certificateValidationCallback;
        }
    }
}