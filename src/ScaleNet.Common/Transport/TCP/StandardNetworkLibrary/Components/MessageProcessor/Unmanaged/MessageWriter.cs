using System;
using System.Runtime.CompilerServices;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageProcessor.Interface;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageProcessor.Unmanaged
{
    internal class MessageWriter : IMessageProcessor
    {
        private int _count;
        private int _offset;
        private int _originalOffset;

        private byte[]? _pendingMessage;
        private int _pendingMessageOffset;
        private int _pendingRemaining;
        private byte[]? _bufferInternal;

        public bool IsHoldingMessage { get; private set; }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                throw new InvalidOperationException("Buffer is not set");

            if (_bufferInternal.Length - _offset >= message.Length)
            {
                fixed (byte* destination = &_bufferInternal[_offset])
                {
                    fixed (byte* msgPin = message)
                    {
                        Buffer.MemoryCopy(msgPin, destination, message.Length, message.Length);
                    }
                }

                _offset += message.Length;
                _count += message.Length;
                return true;
            }

            _pendingMessage = message;
            _pendingMessageOffset = 0;
            _pendingRemaining = _pendingMessage.Length;

            // write whatever you can
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

            if (_pendingRemaining <= _bufferInternal.Length - _offset)
            {
                //System.Buffer.BlockCopy(pendingMessage, pendingMessageOffset, bufferInternal, offset, pendingRemaining);
                fixed (byte* destination = &_bufferInternal[_offset])
                {
                    fixed (byte* msgPin = &_pendingMessage[_pendingMessageOffset])
                    {
                        Buffer.MemoryCopy(msgPin, destination, _pendingRemaining, _pendingRemaining);
                    }
                }

                _offset += _pendingRemaining;
                _count += _pendingRemaining;

                _pendingMessage = null!;
                IsHoldingMessage = false;
                _pendingRemaining = 0;
                _pendingMessageOffset = 0;
                return true;
            }

            //System.Buffer.BlockCopy(pendingMessage, pendingMessageOffset, bufferInternal, offset, bufferInternal.Length - offset);
            fixed (byte* destination = &_bufferInternal[_offset])
            {
                fixed (byte* msgPin = &_pendingMessage[_pendingMessageOffset])
                {
                    Buffer.MemoryCopy(msgPin, destination, _bufferInternal.Length - _offset, _bufferInternal.Length - _offset);
                }
            }

            _count += _bufferInternal.Length - _offset;

            _pendingMessageOffset += _bufferInternal.Length - _offset;
            _pendingRemaining -= _bufferInternal.Length - _offset;
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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