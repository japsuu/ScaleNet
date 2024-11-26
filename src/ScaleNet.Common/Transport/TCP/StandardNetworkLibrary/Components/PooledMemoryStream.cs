using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components
{
    /*There is no allocation here, all byte arrays come from pool and are returned on flush */
    public class PooledMemoryStream : Stream
    {
        private const int ORIGIN = 0;
        private byte[] _bufferInternal;

        private int _length;

        private int _position;


        public PooledMemoryStream()
        {
            _bufferInternal = BufferPool.RentBuffer(512);
        }


        public PooledMemoryStream(int minCapacity)
        {
            _bufferInternal = BufferPool.RentBuffer(minCapacity);
        }


        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;
        private int Capacity => _bufferInternal.Length;

        public override long Position
        {
            get => _position;
            set
            {
                if (_bufferInternal.Length < value)
                    ExpandInternalBuffer((int)value);

                _position = (int)value;
            }
        }

        public int Position32
        {
            get => _position;
            set
            {
                if (_bufferInternal.Length < value)
                    ExpandInternalBuffer(value);

                _position = value;
            }
        }

        public override long Length => _length;


        public override void Flush()
        {
        }


        /// <summary>
        ///     Reduces the inner buffer size, if its more than 65537, to save memory
        /// </summary>
        public void Clear()
        {
            _position = 0;
            if (_bufferInternal.Length > 512000)
            {
                BufferPool.ReturnBuffer(_bufferInternal);
                _bufferInternal = BufferPool.RentBuffer(128000);
            }
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            count = count > Capacity - _position ? Capacity - _position : count;
            unsafe
            {
                fixed (byte* destination = &buffer[offset])
                {
                    fixed (byte* toCopy = &_bufferInternal[_position])
                    {
                        Buffer.MemoryCopy(toCopy, destination, count, count);
                    }
                }

                Position += count;
                return count;
            }
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset > BufferPool.MAX_BUFFER_SIZE)
                throw new ArgumentOutOfRangeException(nameof(offset));
            switch (origin)
            {
                case SeekOrigin.Begin:
                {
                    int tempPosition = unchecked((int)offset);
                    if (offset < 0 || tempPosition < 0)
                        throw new IOException("IO.IO_SeekBeforeBegin");
                    Position = tempPosition;
                    break;
                }
                case SeekOrigin.Current:
                {
                    int tempPosition = unchecked(_position + (int)offset);
                    if (unchecked(_position + offset) < ORIGIN || tempPosition < ORIGIN)
                        throw new IOException("IO.IO_SeekBeforeBegin");
                    Position = tempPosition;
                    break;
                }
                case SeekOrigin.End:
                {
                    int tempPosition = unchecked(_length + (int)offset);
                    if (unchecked(_length + offset) < ORIGIN || tempPosition < ORIGIN)
                        throw new IOException("IO.IO_SeekBeforeBegin");
                    Position = tempPosition;
                    break;
                }
                default:
                    throw new ArgumentException("Argument_InvalidSeekOrigin");
            }


            return _position;
        }


        /// <summary>
        ///     Sets the length of current stream, allocates more memory if length exceeds current inner buffer size.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes.</param>
        public override void SetLength(long value)
        {
            if (Capacity < value)
                ExpandInternalBuffer((int)value);
            _length = (int)value;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] GetBuffer() => _bufferInternal;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return;
            if (_bufferInternal.Length - _position < count)
            {
                int demandSize = count + _bufferInternal.Length;
                if (demandSize > BufferPool.MAX_BUFFER_SIZE)
                    throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
                ExpandInternalBuffer(demandSize); // this at least doubles the buffer 
            }


            unsafe
            {
                fixed (byte* destination = &_bufferInternal[_position])
                {
                    fixed (byte* toCopy = &buffer[offset])
                    {
                        Buffer.MemoryCopy(toCopy, destination, count, count);
                    }
                }
            }

            _position += count;

            if (_length < _position)
                _length = _position;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpandInternalBuffer(int size)
        {
            if (size <= _bufferInternal.Length)
                throw new InvalidOperationException("Cannot expand internal buffer to smaller size");


            byte[] newBuf = BufferPool.RentBuffer(size);
            if (_position > 0)
            {
                unsafe
                {
                    fixed (byte* destination = newBuf)
                    {
                        fixed (byte* toCopy = _bufferInternal)
                        {
                            Buffer.MemoryCopy(toCopy, destination, _position, _position);
                        }
                    }
                }
            }

            BufferPool.ReturnBuffer(_bufferInternal);
            _bufferInternal = newBuf;
        }


        /// <summary>
        ///     Reserves a stream buffer capacity by at least the specified count.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <exception cref="System.InvalidOperationException">
        ///     Cannot expand internal buffer to more than max amount:
        ///     {BufferPool.MaxBufferSize}
        /// </exception>
        public void Reserve(int count)
        {
            if (_bufferInternal.Length - _position < count)
            {
                int demandSize = count + _bufferInternal.Length;
                if (demandSize > BufferPool.MAX_BUFFER_SIZE)
                    throw new InvalidOperationException($"Cannot expand internal buffer to more than max amount: {BufferPool.MAX_BUFFER_SIZE}");
                ExpandInternalBuffer(demandSize);
            }
        }


        public override void WriteByte(byte value)
        {
            if (_bufferInternal.Length - _position < 1)
            {
                int demandSize = 1 + _bufferInternal.Length;
                if (demandSize > BufferPool.MAX_BUFFER_SIZE)
                    throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
                ExpandInternalBuffer(demandSize); // this at least doubles the buffer 
            }

            _bufferInternal[_position++] = value;
            if (_length < _position)
                _length = _position;
        }


        public override int ReadByte()
        {
            if (_position >= _length)
                return -1;
            return _bufferInternal[_position++];
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteIntUnchecked(int value)
        {
            unsafe
            {
                fixed (byte* b = &_bufferInternal[_position])
                {
                    *(int*)b = value;
                }
            }

            _position += 4;
        }


        /// <summary>
        ///     Writes int32 value to stream.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="System.InvalidOperationException">Cannot expand internal buffer to more than max amount</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteInt(int value)
        {
            if (_bufferInternal.Length - _position < 4)
            {
                int demandSize = 4 + _bufferInternal.Length;
                if (demandSize > BufferPool.MAX_BUFFER_SIZE)
                    throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
                ExpandInternalBuffer(demandSize); // this at least doubles the buffer 
            }

            unsafe
            {
                fixed (byte* b = &_bufferInternal[_position])
                {
                    *(int*)b = value;
                }
            }

            _position += 4;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteUshortUnchecked(ushort value)
        {
            unsafe
            {
                fixed (byte* b = &_bufferInternal[_position])
                {
                    *(short*)b = (short)value;
                }
            }

            _position += 2;
        }


        /// <summary>
        ///     Writes the ushort value to the stream.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <exception cref="System.InvalidOperationException">Cannot expand internal buffer to more than max amount</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUshort(ushort value)
        {
            if (_bufferInternal.Length - _position < 2)
            {
                int demandSize = 2 + _bufferInternal.Length;
                if (demandSize > BufferPool.MAX_BUFFER_SIZE)
                    throw new InvalidOperationException("Cannot expand internal buffer to more than max amount");
                ExpandInternalBuffer(demandSize); // this at least doubles the buffer 
            }

            unsafe
            {
                fixed (byte* b = &_bufferInternal[_position])
                {
                    *(short*)b = (short)value;
                }
            }

            _position += 2;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteTwoZerosUnchecked()
        {
            _bufferInternal[_position] = 0;
            _bufferInternal[_position + 1] = 0;
            _position += 2;
        }


        /// <summary>
        ///     Gets a memory region from stream internal buffer, after the current position.
        ///     Size is atleast the amount hinted, and minimum is 256 bytes
        /// </summary>
        /// <param name="sizeHint"></param>
        /// <param name="buff"></param>
        /// <param name="offst"></param>
        /// <param name="cnt"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void GetMemory(int sizeHint, out byte[] buff, out int offst, out int cnt)
        {
            if (sizeHint < 128)
                sizeHint = 128;

            if (_bufferInternal.Length - _position < sizeHint)
            {
                int demandSize = sizeHint + _bufferInternal.Length;
                if (demandSize > BufferPool.MAX_BUFFER_SIZE)
                    throw new InvalidOperationException($"Cannot expand internal buffer to more than max amount: {BufferPool.MAX_BUFFER_SIZE}");
                ExpandInternalBuffer(demandSize);
            }

            buff = _bufferInternal;
            offst = _position;
            cnt = sizeHint;
        }


        internal void Advance(int amount)
        {
            _position += amount;
            if (_length < _position)
                _length = _position;
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                byte[]? buf = Interlocked.Exchange(ref _bufferInternal!, null);
                if (buf != null)
                    BufferPool.ReturnBuffer(buf);
            }

            base.Dispose(disposing);
        }
    }
}