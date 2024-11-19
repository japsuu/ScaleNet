namespace Server;

public static class Logger
{
    public static void LogInfo(string message)
    {
        WriteColored(message, ConsoleColor.White, ConsoleColor.Black);
    }
    
    
    public static void LogWarning(string message)
    {
        WriteColored(message, ConsoleColor.Yellow, ConsoleColor.Black);
    }


    public static void LogError(string message)
    {
        WriteColored(message, ConsoleColor.Red, ConsoleColor.Black);
    }


    private static void WriteColored(string message, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
    {
        ConsoleColor fgCache = Console.ForegroundColor;
        ConsoleColor bgCache = Console.BackgroundColor;
        Console.ForegroundColor = foregroundColor;
        Console.BackgroundColor = backgroundColor;
        
        Console.WriteLine(message);
        
        Console.ForegroundColor = fgCache;
        Console.BackgroundColor = bgCache;
    }
}