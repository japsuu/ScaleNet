using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ScaleNet.Common.Transport.Components
{
    // statefully parse byte messages with 4 byte length header,
    // under any fragmentation condition
    public class ByteMessageReader
    {
        public const int HEADER_LENGTH = 4;
        private readonly byte[] _headerBuffer;
        private readonly int _originalCapacity;

        private bool _awaitingHeader;
        private int _currentExpectedByteLenght;
        private int _currentHeaderBufferPosition;
        private int _currentMsgBufferPosition;
        private int _expectedMsgLenght;
        private byte[] _internalBuffer;


        public ByteMessageReader(int bufferSize = 256000)
        {
            _awaitingHeader = true;
            _currentExpectedByteLenght = 4;

            _headerBuffer = new byte[HEADER_LENGTH];
            _originalCapacity = bufferSize;
            _internalBuffer = BufferPool.RentBuffer(BufferPool.MIN_BUFFER_SIZE);

            _currentMsgBufferPosition = 0;
        }


        public event Action<byte[], int, int>? OnMessageReady;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ParseBytes(byte[] bytes, int offset, int count)
        {
            HandleBytes(bytes, offset, count);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleBytes(byte[] incomingBytes, int offset, int count)
        {
            if (_awaitingHeader)
                HandleHeader(incomingBytes, offset, count);
            else
                HandleBody(incomingBytes, offset, count);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleHeader(byte[] incomingBytes, int offset, int count)
        {
            if (count >= _currentExpectedByteLenght)
            {
                if (_currentHeaderBufferPosition != 0)
                    AppendHeaderChunk(incomingBytes, offset, _currentExpectedByteLenght);
                else
                    AppendHeader(incomingBytes, offset);

                // perfect msg - a hot path here
                if (count - _currentExpectedByteLenght == _expectedMsgLenght)
                {
                    MessageReady(incomingBytes, _currentExpectedByteLenght, _expectedMsgLenght);
                    Reset();
                }

                // multiple msgs or partial incomplete msg.
                else
                {
                    offset += _currentExpectedByteLenght;
                    count -= _currentExpectedByteLenght;
                    _awaitingHeader = false;

                    _currentExpectedByteLenght = _expectedMsgLenght;
                    HandleBody(incomingBytes, offset, count);
                }
            }

            // Fragmented header. we will get on next call,
            else
            {
                AppendHeaderChunk(incomingBytes, offset, count);
                _currentExpectedByteLenght -= count;
            }
        }


        // 0 or more bodies 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleBody(byte[] incomingBytes, int offset, int count)
        {
            int remaining = count;

            // overflown message, there is for sure the message inside
            while (remaining >= _currentExpectedByteLenght)
            {
                // nothing from prev call
                if (_currentMsgBufferPosition == 0)
                {
                    MessageReady(incomingBytes, offset, _currentExpectedByteLenght);
                    Reset();
                }

                // we had partial msg letover
                else
                {
                    AppendMessageChunk(incomingBytes, offset, _currentExpectedByteLenght);
                    MessageReady(_internalBuffer, 0, _currentMsgBufferPosition);

                    // call with false if mem no concern.
                    Reset(true);
                }

                offset += _currentExpectedByteLenght;
                remaining -= _currentExpectedByteLenght;

                // read byte frame and determine next msg.
                if (remaining >= 4)
                {
                    _expectedMsgLenght = BitConverter.ToInt32(incomingBytes, offset);
                    _currentExpectedByteLenght = _expectedMsgLenght;
                    offset += 4;
                    remaining -= 4;
                }

                // incomplete byte frame, we need to store the bytes 
                else if (remaining != 0)
                {
                    AppendHeaderChunk(incomingBytes, offset, remaining);
                    _currentExpectedByteLenght = 4 - remaining;

                    _awaitingHeader = true;
                    return;
                }

                // nothing to store
                else
                {
                    _currentExpectedByteLenght = 4;
                    _awaitingHeader = true;
                    return;
                }
            }

            if (_internalBuffer.Length < _expectedMsgLenght)
            {
                BufferPool.ReturnBuffer(_internalBuffer);
                _internalBuffer = BufferPool.RentBuffer(_expectedMsgLenght);
            }

            // we got the header, but we have a partial msg.
            if (remaining > 0)
            {
                AppendMessageChunk(incomingBytes, offset, remaining);
                _currentExpectedByteLenght = _currentExpectedByteLenght - remaining;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MessageReady(byte[] byteMsg, int offset, int count)
        {
            OnMessageReady?.Invoke(byteMsg, offset, count);
        }


#region Helper

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendMessageChunk(byte[] bytes, int offset, int count)
        {
            //Buffer.BlockCopy(bytes, offset, internalBufer, currentMsgBufferPosition, count);

            unsafe
            {
                fixed (byte* destination = &_internalBuffer[_currentMsgBufferPosition])
                {
                    fixed (byte* message = &bytes[offset])
                    {
                        Buffer.MemoryCopy(message, destination, count, count);
                    }
                }
            }

            _currentMsgBufferPosition += count;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendHeaderChunk(byte[] headerPart, int offset, int count)
        {
            for (int i = 0; i < count; i++)
                _headerBuffer[_currentHeaderBufferPosition++] = headerPart[i + offset];
            if (_currentHeaderBufferPosition == HEADER_LENGTH)
            {
                _expectedMsgLenght = BitConverter.ToInt32(_headerBuffer, offset);
                if (_internalBuffer.Length < _expectedMsgLenght)
                {
                    BufferPool.ReturnBuffer(_internalBuffer);
                    _internalBuffer = BufferPool.RentBuffer(_expectedMsgLenght);
                }
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AppendHeader(byte[] buffer, int offset)
        {
            _expectedMsgLenght = BitConverter.ToInt32(buffer, offset);
            if (_internalBuffer.Length < _expectedMsgLenght)
            {
                BufferPool.ReturnBuffer(_internalBuffer);
                _internalBuffer = BufferPool.RentBuffer(_expectedMsgLenght);
            }

            return _expectedMsgLenght;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Reset(bool freeMemory = false)
        {
            _currentHeaderBufferPosition = 0;
            _currentMsgBufferPosition = 0;
            _expectedMsgLenght = 0;
            if (freeMemory && _internalBuffer.Length > _originalCapacity * 2)
                FreeMemory();
        }


        private void FreeMemory()
        {
            BufferPool.ReturnBuffer(_internalBuffer);
            _internalBuffer = BufferPool.RentBuffer(_originalCapacity);
        }


        public void ReleaseResources()
        {
            OnMessageReady = null;

            byte[]? b = Interlocked.Exchange(ref _internalBuffer!, null);

            if (b != null)
                BufferPool.ReturnBuffer(b);
        }

#endregion
    }
}