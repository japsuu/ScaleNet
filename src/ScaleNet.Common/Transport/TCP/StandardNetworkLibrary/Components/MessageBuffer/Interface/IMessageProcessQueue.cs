using System;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageBuffer.Interface
{
    public interface IMessageQueue : IDisposable
    {
        int CurrentIndexedMemory { get; }
        long TotalMessageDispatched { get; }


        /// <summary>
        /// Enqueues the message if there is enough space available
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>true if the message was enqueued.</returns>
        bool TryEnqueueMessage(byte[] bytes);


        /// <summary>
        ///     Enqueues the message if there is enough space available
        /// </summary>
        /// <returns>true if the message was enqueued.</returns>
        bool TryEnqueueMessage(byte[] bytes, int offset, int count);


        /// <summary>
        /// Flushes the queue if there is anything to flush.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="amountWritten"></param>
        /// <returns>true if something successfully flushed.</returns>
        bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten);


        bool IsEmpty();


        void Flush();
    }
}