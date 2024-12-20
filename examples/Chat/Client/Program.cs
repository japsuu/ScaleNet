using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using ScaleNet.Client.LowLevel;
using Shared;

namespace Client;

internal static class Program
{
    private const string DEFAULT_ADDRESS = "127.0.0.1";
    private const ushort DEFAULT_PORT = SharedConstants.SERVER_PORT;
    
    
    private static void Main(string[] args)
    {
        Console.Title = "Chat Client";

        (string address, ushort port) = GetAddressAndPort(args);
        
        // Create and prepare a new SSL server context
        ClientSslContext context = new(new X509Certificate2(
                "assets/localhost.pfx",
                "yourpassword"),
            TestingCertificateValidationCallback);

        using ChatClient client = new(context, address, port);
        
        // Start the blocking client loop
        client.Run();

        Console.WriteLine("Press any key to exit.");
        Console.ReadKey();
    }


    private static (string, ushort) GetAddressAndPort(string[] args)
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
        if (string.IsNullOrEmpty(portStr) || !ushort.TryParse(portStr, out ushort port))
            port = DEFAULT_PORT;
        return (address, port);
    }


    private static bool TestingCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        // This is a testing callback that allows any certificate.
        return true;
    }
}