using ScaleNet.Logging;

namespace ScaleNet
{
    public static class Networking
    {
        public static bool IsInitialized { get; private set; }
        public static Logger Logger { get; private set; } = new DefaultConsoleLogger();
        
        
        public static void Initialize(Logger? logger = null)
        {
            if (IsInitialized)
                return;

            if (logger != null)
                Logger = logger;
            
            NetMessages.Initialize();
            
            IsInitialized = true;
        }
    }
}