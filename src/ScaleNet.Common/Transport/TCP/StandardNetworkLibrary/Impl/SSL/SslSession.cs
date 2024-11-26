using System;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Threading;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Base.Core;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageBuffer;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageBuffer.Interface;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageProcessor.Unmanaged;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.Statistics;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Utils;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.SSL
{
    public class SslSession : IAsyncSession
    {
        private int _disposedValue;
        protected readonly Spinlock EnqueueLock = new();
        public int MaxIndexedMemory = 128000000;

        protected IMessageQueue MessageQueue = null!;
        protected byte[] ReceiveBuffer = null!;
        public int ReceiveBufferSize = 128000;

        protected IPEndPoint? RemoteEp;

        protected byte[] SendBuffer = null!;
        public int SendBufferSize = 128000;
        protected readonly Spinlock SendSemaphore = new();
        private int _sessionClosing;
        protected Guid SessionId;
        protected readonly SslStream SessionStream;


        public bool DropOnCongestion { get; internal set; }
        public event Action<Guid, byte[], int, int>? BytesReceived;
        public event Action<Guid>? SessionClosed;

        public IPEndPoint RemoteEndpoint
        {
            get => RemoteEp ?? throw new InvalidOperationException("Remote endpoint is not set");
            set => RemoteEp = value;
        }

        private int _started;
        private long _totalBytesReceived;
        private long _totalBytesReceivedPrev;
        private long _totalBytesSend;
        private long _totalBytesSendPrev;
        private long _totalMessageReceived;
        private long _totalMessageSentPrev;
        private long _totalMsgReceivedPrev;

        protected internal bool UseQueue = false;


        public SslSession(Guid sessionId, SslStream sessionStream)
        {
            SessionId = sessionId;
            SessionStream = sessionStream;
        }


        public void StartSession()
        {
            if (Interlocked.Exchange(ref _started, 1) == 0)
            {
                ConfigureBuffers();
                MessageQueue = CreateMessageQueue();
                ThreadPool.UnsafeQueueUserWorkItem(_ => Receive(), null);
            }
        }


        public void SendAsync(byte[] buffer, int offset, int count)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendAsync_(buffer, offset, count);
            }
            catch (Exception e)
            {
                if (!IsSessionClosing())
                {
                    TransportLogger.Log(
                        TransportLogger.LogLevel.Error,
                        $"Unexpected error while sending async with ssl session{e.Message}Trace {e.StackTrace}");
                }
            }
        }


        public void SendAsync(byte[] buffer)
        {
            if (IsSessionClosing())
                return;
            try
            {
                SendAsync_(buffer);
            }
            catch (Exception e)
            {
                if (!IsSessionClosing())
                {
                    TransportLogger.Log(
                        TransportLogger.LogLevel.Error,
                        $"Unexpected error while sending async with ssl session{e.Message}Trace {e.StackTrace}");
                }
            }
        }


        protected virtual void ConfigureBuffers()
        {
            ReceiveBuffer = /*new byte[ReceiveBufferSize];*/ BufferPool.RentBuffer(ReceiveBufferSize);

            //#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER

            //            receiveMemory = new Memory<byte>(receiveBuffer);
            //#endif

            if (UseQueue)
                SendBuffer = BufferPool.RentBuffer(SendBufferSize);
        }


        protected virtual IMessageQueue CreateMessageQueue()
        {
            if (UseQueue)
                return new MessageQueue<MessageWriter>(MaxIndexedMemory, new MessageWriter());
            return new MessageBuffer(MaxIndexedMemory, false);
        }


        private void SendAsync_(byte[] buffer, int offset, int count)
        {
            EnqueueLock.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                return;
            }

            if (SendSemaphore.IsTaken())
            {
                if (MessageQueue.TryEnqueueMessage(buffer, offset, count))
                {
                    EnqueueLock.Release();
                    return;
                }
            }

            EnqueueLock.Release();

            if (DropOnCongestion && SendSemaphore.IsTaken())
                return;

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                SendSemaphore.Release();
                return;
            }

            // you have to push it to queue because queue also does the processing.
            if (!MessageQueue.TryEnqueueMessage(buffer, offset, count))
            {
                TransportLogger.Log(TransportLogger.LogLevel.Error, "Message is too large to fit on buffer");
                EndSession();
                return;
            }

            FlushAndSend();
        }


        private void SendAsync_(byte[] buffer)
        {
            EnqueueLock.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                return;
            }

            if (SendSemaphore.IsTaken())
            {
                if (MessageQueue.TryEnqueueMessage(buffer))
                {
                    EnqueueLock.Release();
                    return;
                }
            }

            EnqueueLock.Release();

            if (DropOnCongestion && SendSemaphore.IsTaken())
                return;

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                SendSemaphore.Release();
                return;
            }

            if (!MessageQueue.TryEnqueueMessage(buffer))
            {
                TransportLogger.Log(TransportLogger.LogLevel.Error, "Message is too large to fit on buffer");
                EndSession();
                return;
            }

            FlushAndSend();
        }


        // this can only be called inside send lock critical section
        protected void FlushAndSend()
        {
            //ThreadPool.UnsafeQueueUserWorkItem((s) => 
            //{
            try
            {
                MessageQueue.TryFlushQueue(ref SendBuffer, 0, out int amountWritten);
                WriteOnSessionStream(amountWritten);
            }
            catch
            {
                if (!IsSessionClosing())
                    throw;
            }

            // }, null);
        }


        protected void WriteOnSessionStream(int count)
        {
            //#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            //            WriteModern(count);
            //            return;
            //#endif

            try
            {
                SessionStream.BeginWrite(SendBuffer, 0, count, SentInternal, null);
            }
            catch (Exception ex)
            {
                HandleError("While attempting to send an error occured", ex);
            }

            _totalBytesSend += count;
        }


        //#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER

        //        private async void WriteModern(int count)
        //        {
        //            try
        //            {
        //                //somehow faster than while loop...
        //            Top:
        //                totalBytesSend += count;
        //                await sessionStream.WriteAsync(new ReadOnlyMemory<byte>(sendBuffer, 0, count)).ConfigureAwait(false);

        //                if (IsSessionClosing())
        //                {
        //                    ReleaseSendResourcesIdempotent();
        //                    return;
        //                }
        //                if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
        //                {
        //                    count = amountWritten;
        //                    goto Top;

        //                }

        //                // here there was nothing to flush
        //                bool flush = false;

        //                enqueueLock.Take();
        //                // ask again safely
        //                if (messageQueue.IsEmpty())
        //                {
        //                    messageQueue.Flush();

        //                    SendSemaphore.Release();
        //                    enqueueLock.Release();
        //                    if (IsSessionClosing())
        //                        ReleaseSendResourcesIdempotent();
        //                    return;
        //                }
        //                else
        //                {
        //                    flush = true;

        //                }
        //                enqueueLock.Release();

        //                // something got into queue just before i exit, we need to flush it
        //                if (flush)
        //                {
        //                    if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten_))
        //                    {
        //                        count = amountWritten_;
        //                        goto Top;
        //                    }
        //                }
        //            }
        //            catch (Exception e)
        //            {
        //                HandleError("Error on sent callback ssl", e);
        //            }
        //        }
        //#endif


        private void SentInternal(IAsyncResult ar)
        {
            if (ar.CompletedSynchronously)
                ThreadPool.UnsafeQueueUserWorkItem(_ => Sent(ar), null);
            else
                Sent(ar);
        }


        private void Sent(IAsyncResult ar)
        {
            try
            {
                if (IsSessionClosing())
                {
                    ReleaseSendResourcesIdempotent();
                    return;
                }

                try
                {
                    SessionStream.EndWrite(ar);
                }
                catch (Exception e)
                {
                    HandleError("While attempting to end async send operation on ssl socket, an error occured", e);
                    ReleaseSendResourcesIdempotent();
                    return;
                }

                if (MessageQueue.TryFlushQueue(ref SendBuffer, 0, out int amountWritten))
                {
                    WriteOnSessionStream(amountWritten);
                    return;
                }

                // here there was nothing to flush

                EnqueueLock.Take();

                // ask again safely
                if (MessageQueue.IsEmpty())
                {
                    MessageQueue.Flush();

                    SendSemaphore.Release();
                    EnqueueLock.Release();
                    if (IsSessionClosing())
                        ReleaseSendResourcesIdempotent();
                    return;
                }

                EnqueueLock.Release();

                // something got into the queue just before exit, we need to flush it
                if (MessageQueue.TryFlushQueue(ref SendBuffer, 0, out int amountWrittenTemp))
                    WriteOnSessionStream(amountWrittenTemp);
            }
            catch (Exception e)
            {
                HandleError("Error on sent callback ssl", e);
            }
        }


        protected virtual void Receive()
        {
            //#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            //            ReceiveNew();
            //            return;
            //#endif
            if (IsSessionClosing())
            {
                ReleaseReceiveResourcesIdempotent();
                return;
            }

            try
            {
                SessionStream.BeginRead(ReceiveBuffer, 0, ReceiveBuffer.Length, Received, null);
            }
            catch (Exception ex)
            {
                HandleError("White receiving from SSL socket an error occurred", ex);
                ReleaseReceiveResourcesIdempotent();
            }
        }


        //#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER


        //        private async void ReceiveNew()
        //        {
        //            try
        //            {
        //                while (true)
        //                {
        //                    if (IsSessionClosing())
        //                    {
        //                        ReleaseReceiveResourcesIdempotent();
        //                        return;
        //                    }
        //                    var amountRead = await sessionStream.ReadAsync(receiveMemory).ConfigureAwait(false);
        //                    if (amountRead > 0)
        //                    {
        //                        HandleReceived(receiveBuffer, 0, amountRead);
        //                    }
        //                    else
        //                    {
        //                        EndSession();
        //                        ReleaseReceiveResourcesIdempotent();
        //                    }
        //                    totalBytesReceived += amountRead;
        //                }
        //            }
        //            catch (Exception ex)
        //            {
        //                HandleError("White receiving from SSL socket an error occurred", ex);
        //                ReleaseReceiveResourcesIdempotent();
        //            }
        //        }
        //#endif
        protected virtual void Received(IAsyncResult ar)
        {
            if (IsSessionClosing())
            {
                ReleaseReceiveResourcesIdempotent();
                return;
            }

            int amountRead;
            try
            {
                amountRead = SessionStream.EndRead(ar);
            }
            catch (Exception e)
            {
                HandleError("While receiving from SSL socket an exception occurred ", e);
                ReleaseReceiveResourcesIdempotent();
                return;
            }

            if (amountRead > 0)
                HandleReceived(ReceiveBuffer, 0, amountRead);
            else
            {
                EndSession();
                ReleaseReceiveResourcesIdempotent();
            }

            _totalBytesReceived += amountRead;

            // Stack overflow prevention.
            if (ar.CompletedSynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem(_ => Receive(), null);
                return;
            }

            Receive();
        }


        protected virtual void HandleReceived(byte[] buffer, int offset, int count)
        {
            _totalMessageReceived++;
            BytesReceived?.Invoke(SessionId, buffer, offset, count);
        }


#region Closure & Disposal

        protected virtual void HandleError(string context, Exception e)
        {
            if (IsSessionClosing())
                return;
            TransportLogger.Log(TransportLogger.LogLevel.Error, $"Context : {context} Message : {e.Message}");
            EndSession();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsSessionClosing() => Interlocked.CompareExchange(ref _sessionClosing, 1, 1) == 1;


        // This method is Idempotent
        public void EndSession()
        {
            if (Interlocked.CompareExchange(ref _sessionClosing, 1, 0) != 0)
                return;
            
            try
            {
                SessionStream.Close();
            }
            catch
            {
                // ignored
            }

            try
            {
                SessionClosed?.Invoke(SessionId);
            }
            catch
            {
                // ignored
            }

            SessionClosed = null;
            Dispose();
        }


        private int _sendResReleased;


        protected void ReleaseSendResourcesIdempotent()
        {
            if (Interlocked.CompareExchange(ref _sendResReleased, 1, 0) == 0)
                ReleaseSendResources();
        }


        protected virtual void ReleaseSendResources()
        {
            try
            {
                if (UseQueue)
                    BufferPool.ReturnBuffer(SendBuffer);

                Interlocked.Exchange(ref MessageQueue!, null)?.Dispose();
            }
            catch (Exception e)
            {
                TransportLogger.Log(
                    TransportLogger.LogLevel.Error,
                    $"Error occurred while releasing ssl session send resources:{e.Message}");
            }
            finally
            {
                EnqueueLock.Release();
            }
        }


        private int _receiveResReleased;


        private void ReleaseReceiveResourcesIdempotent()
        {
            if (Interlocked.CompareExchange(ref _receiveResReleased, 1, 0) == 0)
                ReleaseReceiveResources();
        }


        protected virtual void ReleaseReceiveResources()
        {
            BufferPool.ReturnBuffer(ReceiveBuffer);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposedValue, 1) != 0)
                return;
            
            try
            {
                SessionStream.Close();
                SessionStream.Dispose();
            }
            catch
            {
                // ignored
            }

            BytesReceived = null;

            if (!SendSemaphore.IsTaken())
                ReleaseSendResourcesIdempotent();

            EnqueueLock.Release();
            SendSemaphore.Release();
        }


        public void Dispose()
        {
            Dispose(true);
        }


        public SessionStatistics GetSessionStatistics()
        {
            long deltaReceived = _totalBytesReceived - _totalBytesReceivedPrev;
            long deltaSent = _totalBytesSend - _totalBytesSendPrev;
            _totalBytesSendPrev = _totalBytesSend;
            _totalBytesReceivedPrev = _totalBytesReceived;

            long deltaMSgReceived = _totalMessageReceived - _totalMsgReceivedPrev;
            long deltaMsgSent = MessageQueue.TotalMessageDispatched - _totalMessageSentPrev;

            _totalMsgReceivedPrev = _totalMessageReceived;
            _totalMessageSentPrev = MessageQueue.TotalMessageDispatched;

            return new SessionStatistics(
                MessageQueue.CurrentIndexedMemory,
                // ReSharper disable once PossibleLossOfFraction
                MessageQueue.CurrentIndexedMemory / MaxIndexedMemory,
                _totalBytesReceived,
                _totalBytesSend,
                deltaSent,
                deltaReceived,
                MessageQueue.TotalMessageDispatched,
                _totalMessageReceived,
                deltaMsgSent,
                deltaMSgReceived);
        }

#endregion
    }
}