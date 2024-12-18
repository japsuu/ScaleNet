using System.Security.Authentication;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.Core
{

    [System.Serializable]
    public struct SslConfigurationOld
    {
        public bool Enabled;
        public string CertificatePath;
        public string CertificatePassword;
        public SslProtocols SslProtocol;

        public SslConfigurationOld(bool enabled, string certPath, string certPassword, SslProtocols sslProtocols)
        {
            Enabled = enabled;
            CertificatePath = certPath;
            CertificatePassword = certPassword;
            SslProtocol = sslProtocols;
        }
    }

}