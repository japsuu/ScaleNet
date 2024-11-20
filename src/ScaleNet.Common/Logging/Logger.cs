using System.Diagnostics;

namespace ScaleNet.Common.Logging
{
    /// <summary>
    /// Represents a logger that can be used to log messages at different log levels.
    /// </summary>
    ///
    /// <remarks>
    /// Implementations of this interface should be thread-safe.
    /// </remarks>
    public abstract class Logger
    {
        public abstract LogLevel LogLevel { get; set; }
        
        [Conditional("DEBUG")]
        public abstract void LogDebug(string message);
        public abstract void LogInfo(string message);
        public abstract void LogWarning(string message);
        public abstract void LogError(string message);
    }
}