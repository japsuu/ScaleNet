using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ScaleNet.Common.Ssl;
using Shared;

namespace Client;

internal static class Program
{
    private const string DEFAULT_ADDRESS = "127.0.0.1";
    private const int DEFAULT_PORT = SharedConstants.SERVER_PORT;
    
    
    private static void Main(string[] args)
    {
        Console.Title = "COV Client";

        (string address, int port) = GetAddressAndPort(args);
        
        // Create and prepare a new SSL server context
        SslContext context = new SslContext(SslProtocols.Tls12, new X509Certificate2(
                "assets/localhost.pfx",
                "yourpassword"),
            TestingCertificateValidationCallback);

        GameClient client = new(context, address, port);
        
        // Start the blocking client loop
        client.Run();

        Console.WriteLine("Press any key to exit.");
        Console.ReadKey();
    }


    private static (string, int) GetAddressAndPort(string[] args)
    {
        string? address = null;
        string? portStr = null;
        
        // Try to read address & port from args in address:port format
        if (args.Length > 0)
        {
            string[] parts = args[0].Split(':');
            if (parts.Length == 2)
            {
                address = parts[0];
                portStr = parts[1];
            }
        }
        
        if (string.IsNullOrEmpty(address))
        {
            Console.WriteLine("Enter the server address:");
            address = Console.ReadLine();
        }
        if (string.IsNullOrEmpty(address))
            address = DEFAULT_ADDRESS;

        if (string.IsNullOrEmpty(portStr))
        {
            Console.WriteLine("Enter the server port:");
            portStr = Console.ReadLine();
        }
        if (string.IsNullOrEmpty(portStr) || !int.TryParse(portStr, out int port))
            port = DEFAULT_PORT;
        return (address, port);
    }


    private static bool TestingCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        // This is a testing callback that allows any certificate.
        return true;
    }
}