using System;
using System.Runtime.CompilerServices;
using ScaleNet.Common.Transport.Components.MessageProcessor.Interface;

namespace ScaleNet.Common.Transport.Components.MessageProcessor.Unmanaged
{
    internal sealed class DelimitedMessageWriter : IMessageProcessor
    {
        private byte[]? _bufferInternal;
        private int _count;
        private int _offset;
        private int _originalOffset;

        private byte[]? _pendingMessage;
        private int _pendingMessageOffset;
        private int _pendingRemaining;
        private bool _writeHeaderOnFlush;

        public bool IsHoldingMessage { get; private set; }


        public void SetBuffer(ref byte[] buffer, int offset)
        {
            _bufferInternal = buffer;
            _offset = offset;
            _originalOffset = offset;
            _count = 0;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool ProcessMessage(byte[] message)
        {
            if (IsHoldingMessage)
                throw new InvalidOperationException("You can not process new message before heldover message is fully flushed");
            if (_bufferInternal == null)
                return false;
            if (_bufferInternal.Length - _offset >= 36)
            {
                fixed (byte* b = &_bufferInternal[_offset])
                {
                    *(int*)b = message.Length;
                }

                _offset += 4;
                _count += 4;
            }
            else
            {
                _writeHeaderOnFlush = true;
                _pendingMessage = message;
                _pendingMessageOffset = 0;
                _pendingRemaining = _pendingMessage.Length;
                IsHoldingMessage = true;
                return false;
            }

            if (_bufferInternal.Length - _offset >= message.Length)
            {
                fixed (byte* destination = &_bufferInternal[_offset])
                {
                    fixed (byte* msgPtr = message)
                    {
                        Buffer.MemoryCopy(msgPtr, destination, message.Length, message.Length);
                    }
                }

                _offset += message.Length;
                _count += message.Length;
                return true;
            }

            _pendingMessage = message;
            _pendingMessageOffset = 0;
            _pendingRemaining = _pendingMessage.Length;
            _ = Flush();
            IsHoldingMessage = true;
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Flush()
        {
            if (_bufferInternal == null)
                throw new InvalidOperationException("Buffer is not set");
            
            if (_pendingMessage == null)
                throw new InvalidOperationException("There is no message to flush");
            
            if (_writeHeaderOnFlush)
            {
                
                _writeHeaderOnFlush = false;
                fixed (byte* b = &_bufferInternal[_offset])
                {
                    *(int*)b = _pendingMessage.Length;
                }

                _offset += 4;
                _count += 4;
            }

            if (_pendingRemaining <= _bufferInternal.Length - _offset)
            {
                fixed (byte* destination = &_bufferInternal[_offset])
                {
                    fixed (byte* msgPtr = &_pendingMessage[_pendingMessageOffset])
                    {
                        Buffer.MemoryCopy(msgPtr, destination, _pendingRemaining, _pendingRemaining);
                    }
                }

                _offset += _pendingRemaining;
                _count += _pendingRemaining;

                _pendingMessage = null!;
                _pendingRemaining = 0;
                _pendingMessageOffset = 0;
                IsHoldingMessage = false;
                return true;
            }

            fixed (byte* destination = &_bufferInternal[_offset])
            {
                fixed (byte* msgPtr = &_pendingMessage[_pendingMessageOffset])
                {
                    Buffer.MemoryCopy(msgPtr, destination, _bufferInternal.Length - _offset, _bufferInternal.Length - _offset);
                }
            }

            _count += _bufferInternal.Length - _offset;

            _pendingMessageOffset += _bufferInternal.Length - _offset;
            _pendingRemaining -= _bufferInternal.Length - _offset;
            return false;
        }


        public void GetBuffer(out byte[] buffer, out int offset, out int count)
        {
            buffer = _bufferInternal ?? throw new InvalidOperationException("Buffer is not set");
            offset = _originalOffset;
            count = _count;
        }


        public void Dispose()
        {
        }
    }
}