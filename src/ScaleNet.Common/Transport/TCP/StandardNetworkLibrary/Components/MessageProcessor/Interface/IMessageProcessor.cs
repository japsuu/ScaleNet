using System;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageProcessor.Interface
{
    public interface IMessageProcessor : IDisposable
    {
        bool IsHoldingMessage { get; }

        
        /// <summary>
        /// Sets a buffer where the messages will be processed into
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        void SetBuffer(ref byte[] buffer, int offset);


        /// <summary>
        /// Processes a given message into a buffer set by <see cref="SetBuffer" />
        /// </summary>
        /// <param name="message"></param>
        /// <returns>
        /// true, if the message is completely processed, false means the message was partially processed and a flush is required
        /// </returns>
        bool ProcessMessage(byte[] message);


        /// <summary>
        /// Flushes a heldover message into buffer set by <see cref="SetBuffer" />.
        /// </summary>
        /// <returns>true if heldover message is completely flushed, false if the message isn't fully processed.</returns>
        bool Flush();


        /// <summary>
        /// Returns the buffer set by <see cref="SetBuffer" />
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        void GetBuffer(out byte[] buffer, out int offset, out int count);
    }
}