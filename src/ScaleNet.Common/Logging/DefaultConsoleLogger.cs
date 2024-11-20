using System;

namespace ScaleNet.Common.Logging
{
    internal sealed class DefaultConsoleLogger : Logger
    {
        /// <summary>
        /// The current log level threshold.
        /// The lower the level, the more messages are logged.
        /// </summary>
        public override LogLevel LogLevel { get; set; } = LogLevel.INFO;
    
    
        public override void LogDebug(string message)
        {
            WriteColored(LogLevel.DEBUG, message, ConsoleColor.Gray, ConsoleColor.Black);
        }
    
    
        public override void LogInfo(string message)
        {
            WriteColored(LogLevel.INFO, message, ConsoleColor.White, ConsoleColor.Black);
        }
    
    
        public override void LogWarning(string message)
        {
            WriteColored(LogLevel.WARNING, message, ConsoleColor.Yellow, ConsoleColor.Black);
        }


        public override void LogError(string message)
        {
            WriteColored(LogLevel.ERROR, message, ConsoleColor.Red, ConsoleColor.Black);
        }


        private void WriteColored(LogLevel level, string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
            if (level < LogLevel)
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
}