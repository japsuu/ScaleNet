using System;
using ScaleNet.Common;
using Conditional = System.Diagnostics.ConditionalAttribute;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common
{
    internal static class SimpleWebLog
    {
        // used for Conditional
        private const string SIMPLEWEB_LOG_ENABLED = nameof(SIMPLEWEB_LOG_ENABLED);
        private const string DEBUG = nameof(DEBUG);

        public static string BufferToString(byte[] buffer, int offset = 0, int? length = null) => BitConverter.ToString(buffer, offset, length ?? buffer.Length);


        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void DumpBuffer(string label, byte[] buffer, int offset, int length)
        {
            ScaleNetManager.Logger.LogDebug($"VERBOSE: {label}: {BufferToString(buffer, offset, length)}");
        }


        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void DumpBuffer(string label, ArrayBuffer arrayBuffer)
        {
            ScaleNetManager.Logger.LogDebug($"VERBOSE: {label}: {BufferToString(arrayBuffer.Array, 0, arrayBuffer.Count)}");
        }


        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void Verbose(string msg)
        {
            ScaleNetManager.Logger.LogDebug($"VERBOSE: {msg}");
        }


        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void Info(string msg)
        {
            ScaleNetManager.Logger.LogInfo($"INFO: {msg}");
        }


        /// <summary>
        /// An expected Exception was caught, useful for debugging but not important
        /// </summary>
        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void InfoException(Exception e)
        {
            ScaleNetManager.Logger.LogInfo($"INFO_EXCEPTION: {e.GetType().Name} Message: {e.Message}\n{e.StackTrace}\n\n");
        }


        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        [Conditional(DEBUG)]
        public static void Warn(string msg, bool showColor = true)
        {
            ScaleNetManager.Logger.LogWarning($"WARN: {msg}");
        }


        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        [Conditional(DEBUG)]
        public static void Error(string msg, bool showColor = true)
        {
            ScaleNetManager.Logger.LogError($"ERROR: {msg}");
        }


        public static void Exception(Exception e)
        {
            // always log Exceptions
            ScaleNetManager.Logger.LogError($"EXCEPTION: <color=red>{e.GetType().Name}</color> Message: {e.Message}\n{e.StackTrace}\n\n");
        }
    }
}