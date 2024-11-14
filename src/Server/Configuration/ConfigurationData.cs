using Shared.Utils;
using YamlDotNet.Serialization;

namespace Server.Configuration;

public class ConfigurationData
{
    [YamlMember(Description = "The maximum number of connections allowed to the server.")]
    public required int MaxConnections { get; init; }


    public static ConfigurationData GetDefault()
    {
        ConfigurationData defaultConfig = new()
        {
            MaxConnections = 1000,
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
        
        return true;
    }
}