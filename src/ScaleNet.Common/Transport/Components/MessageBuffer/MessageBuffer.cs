using System.Threading;
using ScaleNet.Common.Transport.Components.MessageBuffer.Interface;

namespace ScaleNet.Common.Transport.Components.MessageBuffer
{
    public sealed class MessageBuffer : IMessageQueue
    {
        private readonly object _bufferLock = new();
        private int _currentIndexedMemory;
        private bool _disposedValue;
        private PooledMemoryStream _flushStream = new();
        private readonly int _maxIndexedMemory;
        private readonly bool _writeLengthPrefix;

        private PooledMemoryStream _writeStream = new();


        public MessageBuffer(int maxIndexedMemory, bool writeLengthPrefix = true)
        {
            _writeLengthPrefix = writeLengthPrefix;
            _maxIndexedMemory = maxIndexedMemory;
        }


        public int CurrentIndexedMemory => Interlocked.CompareExchange(ref _currentIndexedMemory, 0, 0);
        public long TotalMessageDispatched { get; private set; }

        public bool IsEmpty() => Volatile.Read(ref _disposedValue) || _writeStream.Position == 0;


        public bool TryEnqueueMessage(byte[] bytes)
        {
            lock (_bufferLock)
            {
                if (_currentIndexedMemory < _maxIndexedMemory && !_disposedValue)
                {
                    TotalMessageDispatched++;

                    if (_writeLengthPrefix)
                    {
                        _currentIndexedMemory += 4;
                        _writeStream.WriteInt(bytes.Length);
                    }

                    _writeStream.Write(bytes, 0, bytes.Length);
                    _currentIndexedMemory += bytes.Length;
                    return true;
                }
            }

            return false;
        }


        public bool TryEnqueueMessage(byte[] bytes, int offset, int count)
        {
            lock (_bufferLock)
            {
                if (_currentIndexedMemory < _maxIndexedMemory && !_disposedValue)
                {
                    TotalMessageDispatched++;

                    if (_writeLengthPrefix)
                    {
                        _currentIndexedMemory += 4;
                        _writeStream.WriteInt(count);
                    }

                    _writeStream.Write(bytes, offset, count);
                    _currentIndexedMemory += count;
                    return true;
                }
            }

            return false;
        }


        public bool TryFlushQueue(ref byte[] buffer, int offset, out int amountWritten)
        {
            lock (_bufferLock)
            {
                if (IsEmpty())
                {
                    amountWritten = 0;
                    return false;
                }

                (_writeStream, _flushStream) = (_flushStream, _writeStream);

                buffer = _flushStream.GetBuffer();
                amountWritten = _flushStream.Position32;

                _currentIndexedMemory -= amountWritten;
                _flushStream.Position32 = 0;

                return true;
            }
        }


        public void Dispose()
        {
            Dispose(true);
        }


        public void Flush()
        {
            _flushStream.Clear();
        }


        public bool TryEnqueueMessage(byte[] data1, int offset1, int count1, byte[] data2, int offset2, int count2)
        {
            lock (_bufferLock)
            {
                if (_currentIndexedMemory < _maxIndexedMemory && !_disposedValue)
                {
                    TotalMessageDispatched++;

                    if (_writeLengthPrefix)
                    {
                        _currentIndexedMemory += 4;
                        _writeStream.WriteInt(count1 + count2);
                    }

                    _writeStream.Write(data1, offset1, count1);
                    _writeStream.Write(data2, offset2, count2);
                    _currentIndexedMemory += count1 + count2;
                    return true;
                }
            }

            return false;
        }


        private void Dispose(bool disposing)
        {
            lock (_bufferLock)
            {
                if (!_disposedValue)
                {
                    Volatile.Write(ref _disposedValue, true);
                    if (disposing)
                    {
                        _writeStream.Clear();
                        _flushStream.Clear();
                        _writeStream.Dispose();
                        _flushStream.Dispose();
                    }
                }
            }
        }
    }
}