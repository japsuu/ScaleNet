using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ScaleNet.Server.LowLevel.Transport.Tcp;
using Server.Configuration;
using Shared;

namespace Server;

internal static class Program
{
    private static void Main(string[] args)
    {
        Console.Title = "COV Server";
        if (!ConfigManager.TryLoadConfiguration())
        {
            Console.WriteLine("Failed to load configuration.");
            return;
        }
        
        // Create and prepare a new SSL server context
        SslContext context = new SslContext(SslProtocols.Tls12, new X509Certificate2("server.pfx", ConfigManager.CurrentConfiguration.CertificatePassword));
        
        // Create the server
        GameServer server = new(
            context,
            IPAddress.Any,
            SharedConstants.SERVER_PORT,
            ConfigManager.CurrentConfiguration.MaxConnections,
            ConfigManager.CurrentConfiguration.AllowAccountRegistration);
        Console.WriteLine("startup");
        
        // Start the blocking server loop
        server.Run();
    }
}