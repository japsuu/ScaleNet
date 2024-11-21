using YamlDotNet.Serialization;

namespace Server.Configuration;

public class ConfigurationData
{
    [YamlMember(Description = "The maximum number of connections allowed to the server.")]
    public required int MaxConnections { get; init; }
    
    [YamlMember(Description = "Whether to allow new account registration.")]
    public required bool AllowAccountRegistration { get; init; }

    [YamlMember(Description = "The password for the server certificate.")]
    public required string CertificatePassword { get; init; }


    public static ConfigurationData GetDefault()
    {
        ConfigurationData defaultConfig = new()
        {
            MaxConnections = 1000,
            AllowAccountRegistration = true,
            CertificatePassword = "yourpassword"
        };
        
        return defaultConfig;
    }
    
    
    public static bool Verify(ConfigurationData config)
    {
        if (config.MaxConnections < 1 || config.MaxConnections > 10000)
        {
            Logger.LogError($"Unsupported value: {nameof(MaxConnections)}.");
            return false;
        }
        
        if (string.IsNullOrWhiteSpace(config.CertificatePassword))
        {
            Logger.LogError($"Invalid value: {nameof(CertificatePassword)}.");
            return false;
        }
        
        return true;
    }
}