namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

internal static class Utils
{
    public static void SleepForInterrupt()
    {
        // sleep to check for ThreadInterruptedException
        Thread.Sleep(1);
    }
}