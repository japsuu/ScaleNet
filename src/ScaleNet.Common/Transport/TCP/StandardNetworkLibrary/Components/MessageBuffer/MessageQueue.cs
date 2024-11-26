using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageBuffer.Interface;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageProcessor.Interface;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Utils;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageBuffer
{
    internal sealed class MessageQueue<T> : IMessageQueue where T : IMessageProcessor
    {
        private readonly int _maxIndexedMemory;
        private int _currentIndexedMemory;
        private bool _flushNext;
        private T _processor;

        internal readonly ConcurrentQueue<byte[]> SendQueue = new();


        public MessageQueue(int maxIndexedMemory, T processor)
        {
            _maxIndexedMemory = maxIndexedMemory;
            _processor = processor;
        }


        public int CurrentIndexedMemory => Interlocked.CompareExchange(ref _currentIndexedMemory, 0, 0);
        public long TotalMessageDispatched { get; private set; }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueueMessage(byte[] bytes)
        {
            if (Volatile.Read(ref _currentIndexedMemory) < _maxIndexedMemory)
            {
                Interlocked.Add(ref _currentIndexedMemory, bytes.Length);
                SendQueue.Enqueue(bytes);
                return true;
            }

            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten)
        {
            amountWritten = 0;
            _processor.SetBuffer(ref buffer, offset);
            if (_flushNext)
            {
                if (!_processor.Flush())
                {
                    _processor.GetBuffer(out buffer, out _, out amountWritten);
                    return true;
                }

                _flushNext = false;
            }

            int memcount = 0;
            while (SendQueue.TryDequeue(out byte[] bytes))
            {
                TotalMessageDispatched++;

                memcount += bytes.Length;
                if (_processor.ProcessMessage(bytes))
                    continue;
                _flushNext = true;
                break;
            }

            Interlocked.Add(ref _currentIndexedMemory, -memcount);
            _processor.GetBuffer(out buffer, out _, out amountWritten);
            return amountWritten != 0;
        }


        public bool IsEmpty() => SendQueue.IsEmpty && !_processor.IsHoldingMessage;


        public bool TryEnqueueMessage(byte[] bytes, int offset, int count)
        {
            byte[] array = ByteCopy.ToArray(bytes, offset, count);
            return TryEnqueueMessage(array);
        }


        public void Dispose()
        {
            _processor.Dispose();
        }


        public void Flush()
        {
        }
    }
}