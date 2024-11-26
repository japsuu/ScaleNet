using System;
using System.Net;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Threading;
using ScaleNet.Common.Transport.Components;
using ScaleNet.Common.Transport.Components.MessageBuffer;
using ScaleNet.Common.Transport.Components.MessageBuffer.Interface;
using ScaleNet.Common.Transport.Components.MessageProcessor.Unmanaged;
using ScaleNet.Common.Transport.Components.Statistics;
using ScaleNet.Common.Transport.Tcp.Base.Core;
using ScaleNet.Common.Transport.Utils;

namespace ScaleNet.Common.Transport.Tcp.SSL
{
    public class SslSession : IAsyncSession
    {
        private int disposedValue;
        protected Spinlock enqueueLock = new();
        public int MaxIndexedMemory = 128000000;

        protected IMessageQueue messageQueue;
        protected byte[] receiveBuffer;
        public int ReceiveBufferSize = 128000;

        protected IPEndPoint RemoteEP;

        //#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
        //        protected Memory<byte> receiveMemory;
        //#endif
        protected byte[] sendBuffer;
        public int SendBufferSize = 128000;
        protected Spinlock SendSemaphore = new();
        private int SessionClosing;
        protected Guid sessionId;
        protected SslStream sessionStream;

        private int started;
        private long totalBytesReceived;
        private long totalBytesReceivedPrev;
        private long totalBytesSend;
        private long totalBytesSendPrev;
        private long totalMessageReceived;
        private long totalMessageSentPrev;
        private long totalMsgReceivedPrev;

        protected internal bool UseQueue = false;


        public SslSession(Guid sessionId, SslStream sessionStream)
        {
            this.sessionId = sessionId;
            this.sessionStream = sessionStream;
        }


        public bool DropOnCongestion { get; internal set; }
        public event Action<Guid, byte[], int, int> OnBytesRecieved;
        public event Action<Guid> OnSessionClosed;

        public IPEndPoint RemoteEndpoint
        {
            get => RemoteEP;
            set => RemoteEP = value;
        }


        public void StartSession()
        {
            if (Interlocked.Exchange(ref started, 1) == 0)
            {
                ConfigureBuffers();
                messageQueue = CreateMessageQueue();
                ThreadPool.UnsafeQueueUserWorkItem(s => Receive(), null);
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
                        "Unexpected error while sending async with ssl session" + e.Message + "Trace " + e.StackTrace);
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
                        "Unexpected error while sending async with ssl session" + e.Message + "Trace " + e.StackTrace);
                }
            }
        }


        protected virtual void ConfigureBuffers()
        {
            receiveBuffer = /*new byte[ReceiveBufferSize];*/ BufferPool.RentBuffer(ReceiveBufferSize);

            //#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER

            //            receiveMemory = new Memory<byte>(receiveBuffer);
            //#endif

            if (UseQueue)
                sendBuffer = BufferPool.RentBuffer(SendBufferSize);
        }


        protected virtual IMessageQueue CreateMessageQueue()
        {
            if (UseQueue)
                return new MessageQueue<MessageWriter>(MaxIndexedMemory, new MessageWriter());
            return new MessageBuffer(MaxIndexedMemory, false);
        }


        private void SendAsync_(byte[] buffer, int offset, int count)
        {
            enqueueLock.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                return;
            }

            if (SendSemaphore.IsTaken())
            {
                if (messageQueue.TryEnqueueMessage(buffer, offset, count))
                {
                    enqueueLock.Release();
                    return;
                }
            }

            enqueueLock.Release();

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
            if (!messageQueue.TryEnqueueMessage(buffer, offset, count))
            {
                TransportLogger.Log(TransportLogger.LogLevel.Error, "Message is too large to fit on buffer");
                EndSession();
                return;
            }

            FlushAndSend();
        }


        private void SendAsync_(byte[] buffer)
        {
            enqueueLock.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                return;
            }

            if (SendSemaphore.IsTaken())
            {
                if (messageQueue.TryEnqueueMessage(buffer))
                {
                    enqueueLock.Release();
                    return;
                }
            }

            enqueueLock.Release();

            if (DropOnCongestion && SendSemaphore.IsTaken())
                return;

            SendSemaphore.Take();
            if (IsSessionClosing())
            {
                ReleaseSendResourcesIdempotent();
                SendSemaphore.Release();
                return;
            }

            if (!messageQueue.TryEnqueueMessage(buffer))
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
                messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten);
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
                sessionStream.BeginWrite(sendBuffer, 0, count, SentInternal, null);
            }
            catch (Exception ex)
            {
                HandleError("While attempting to send an error occured", ex);
            }

            totalBytesSend += count;
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
                ThreadPool.UnsafeQueueUserWorkItem(s => Sent(ar), null);
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
                    sessionStream.EndWrite(ar);
                }
                catch (Exception e)
                {
                    HandleError("While attempting to end async send operation on ssl socket, an error occured", e);
                    ReleaseSendResourcesIdempotent();
                    return;
                }

                if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten))
                {
                    WriteOnSessionStream(amountWritten);
                    return;
                }

                // here there was nothing to flush
                bool flush = false;

                enqueueLock.Take();

                // ask again safely
                if (messageQueue.IsEmpty())
                {
                    messageQueue.Flush();

                    SendSemaphore.Release();
                    enqueueLock.Release();
                    if (IsSessionClosing())
                        ReleaseSendResourcesIdempotent();
                    return;
                }

                flush = true;
                enqueueLock.Release();

                // something got into queue just before i exit, we need to flush it
                if (flush)
                {
                    if (messageQueue.TryFlushQueue(ref sendBuffer, 0, out int amountWritten_))
                        WriteOnSessionStream(amountWritten_);
                }
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
                sessionStream.BeginRead(receiveBuffer, 0, receiveBuffer.Length, Received, null);
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

            int amountRead = 0;
            try
            {
                amountRead = sessionStream.EndRead(ar);
            }
            catch (Exception e)
            {
                HandleError("While receiving from SSL socket an exception occurred ", e);
                ReleaseReceiveResourcesIdempotent();
                return;
            }

            if (amountRead > 0)
                HandleReceived(receiveBuffer, 0, amountRead);
            else
            {
                EndSession();
                ReleaseReceiveResourcesIdempotent();
            }

            totalBytesReceived += amountRead;

            // Stack overflow prevention.
            if (ar.CompletedSynchronously)
            {
                ThreadPool.UnsafeQueueUserWorkItem(e => Receive(), null);
                return;
            }

            Receive();
        }


        protected virtual void HandleReceived(byte[] buffer, int offset, int count)
        {
            totalMessageReceived++;
            OnBytesRecieved?.Invoke(sessionId, buffer, offset, count);
        }


#region Closure & Disposal

        protected virtual void HandleError(string context, Exception e)
        {
            if (IsSessionClosing())
                return;
            TransportLogger.Log(TransportLogger.LogLevel.Error, "Context : " + context + " Message : " + e.Message);
            EndSession();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsSessionClosing() => Interlocked.CompareExchange(ref SessionClosing, 1, 1) == 1;


        // This method is Idempotent
        public void EndSession()
        {
            if (Interlocked.CompareExchange(ref SessionClosing, 1, 0) == 0)
            {
                try
                {
                    sessionStream.Close();
                }
                catch
                {
                }

                try
                {
                    OnSessionClosed?.Invoke(sessionId);
                }
                catch
                {
                }

                OnSessionClosed = null;
                Dispose();
            }
        }


        private int sendResReleased;


        protected void ReleaseSendResourcesIdempotent()
        {
            if (Interlocked.CompareExchange(ref sendResReleased, 1, 0) == 0)
                ReleaseSendResources();
        }


        protected virtual void ReleaseSendResources()
        {
            try
            {
                if (UseQueue)
                    BufferPool.ReturnBuffer(sendBuffer);

                Interlocked.Exchange(ref messageQueue, null)?.Dispose();
            }
            catch (Exception e)
            {
                TransportLogger.Log(
                    TransportLogger.LogLevel.Error,
                    "Error eccured while releasing ssl session send resources:" + e.Message);
            }
            finally
            {
                enqueueLock.Release();
            }
        }


        private int receiveResReleased;


        private void ReleaseReceiveResourcesIdempotent()
        {
            if (Interlocked.CompareExchange(ref receiveResReleased, 1, 0) == 0)
                ReleaseReceiveResources();
        }


        protected virtual void ReleaseReceiveResources()
        {
            BufferPool.ReturnBuffer(receiveBuffer);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref disposedValue, 1) == 0)
            {
                try
                {
                    sessionStream.Close();
                    sessionStream.Dispose();
                }
                catch
                {
                }

                OnBytesRecieved = null;

                if (!SendSemaphore.IsTaken())
                    ReleaseSendResourcesIdempotent();

                enqueueLock.Release();
                SendSemaphore.Release();
            }
        }


        public void Dispose()
        {
            Dispose(true);
        }


        public SessionStatistics GetSessionStatistics()
        {
            long deltaReceived = totalBytesReceived - totalBytesReceivedPrev;
            long deltaSent = totalBytesSend - totalBytesSendPrev;
            totalBytesSendPrev = totalBytesSend;
            totalBytesReceivedPrev = totalBytesReceived;

            long deltaMSgReceived = totalMessageReceived - totalMsgReceivedPrev;
            long deltaMsgSent = messageQueue.TotalMessageDispatched - totalMessageSentPrev;

            totalMsgReceivedPrev = totalMessageReceived;
            totalMessageSentPrev = messageQueue.TotalMessageDispatched;

            return new SessionStatistics(
                messageQueue.CurrentIndexedMemory,
                messageQueue.CurrentIndexedMemory / MaxIndexedMemory,
                totalBytesReceived,
                totalBytesSend,
                deltaSent,
                deltaReceived,
                messageQueue.TotalMessageDispatched,
                totalMessageReceived,
                deltaMsgSent,
                deltaMSgReceived);
        }

#endregion
    }
}