using System.Diagnostics;

namespace ScaleNet.Utils;

public static class Logger
{
    public enum LogLevel : byte
    {
        INFO = 0,
        WARNING = 1,
        ERROR = 2,
        FATAL = 3,
        DEBUG = 255
    }
    
    /// <summary>
    /// The current log level threshold.
    /// The lower the level, the more messages are logged.
    /// </summary>
    public static LogLevel CurrentLogLevel { get; set; } = LogLevel.INFO;
    
    
    [Conditional("DEBUG")]
    public static void LogDebug(string message)
    {
        WriteColored(LogLevel.DEBUG, message, ConsoleColor.Gray, ConsoleColor.Black);
    }
    
    
    public static void LogInfo(string message)
    {
        WriteColored(LogLevel.INFO, message, ConsoleColor.White, ConsoleColor.Black);
    }
    
    
    public static void LogWarning(string message)
    {
        WriteColored(LogLevel.WARNING, message, ConsoleColor.Yellow, ConsoleColor.Black);
    }


    public static void LogError(string message)
    {
        WriteColored(LogLevel.ERROR, message, ConsoleColor.Red, ConsoleColor.Black);
    }


    public static void LogException(string message, Exception ex)
    {
        WriteColored(LogLevel.FATAL, message, ConsoleColor.Red, ConsoleColor.Black);
        Console.ForegroundColor = ConsoleColor.Red;
        
        throw ex;
    }


    private static void WriteColored(LogLevel level, string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
    {
        if (level < CurrentLogLevel)
            return;
        
        ConsoleColor fgCache = Console.ForegroundColor;
        ConsoleColor bgCache = Console.BackgroundColor;
        Console.ForegroundColor = foregroundColor;
        Console.BackgroundColor = backgroundColor;
        
        string levelString = level.ToString();
        Console.WriteLine($"[{levelString} - {DateTime.Now}] {message}");
        
        Console.ForegroundColor = fgCache;
        Console.BackgroundColor = bgCache;
    }
}