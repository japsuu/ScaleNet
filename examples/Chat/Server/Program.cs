using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ScaleNet.Server.LowLevel;
using Server.Configuration;
using Shared;

namespace Server;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.Title = "Chat Server";
        if (!ConfigManager.TryLoadConfiguration())
        {
            Console.WriteLine("Failed to load configuration.");
            return;
        }
        
        // Create and prepare a new SSL server context
        ServerSslContext context = new ServerSslContext(new X509Certificate2(
            "assets/localhost.pfx",
            ConfigManager.CurrentConfiguration.CertificatePassword),
            TestingCertificateValidationCallback);
        
        // Create the server
        using ChatServer server = new(
            context,
            IPAddress.Any,
            SharedConstants.SERVER_PORT,
            ConfigManager.CurrentConfiguration.MaxConnections,
            ConfigManager.CurrentConfiguration.AllowAccountRegistration);
        Console.WriteLine("startup");
        
        // Start the blocking server loop
        server.Run();
    }


    private static bool TestingCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        // This is a testing callback that allows any certificate.
        return true;
    }
}