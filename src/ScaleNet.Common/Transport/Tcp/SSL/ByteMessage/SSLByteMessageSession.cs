using System;
using System.Net.Security;
using ScaleNet.Common.Transport.Components;
using ScaleNet.Common.Transport.Components.MessageBuffer;
using ScaleNet.Common.Transport.Components.MessageBuffer.Interface;
using ScaleNet.Common.Transport.Components.MessageProcessor.Unmanaged;

namespace ScaleNet.Common.Transport.Tcp.SSL.ByteMessage
{
    internal class SslByteMessageSession : SslSession
    {
        private readonly ByteMessageReader reader;


        public SslByteMessageSession(Guid sessionId, SslStream sessionStream) : base(sessionId, sessionStream)
        {
            reader = new ByteMessageReader();
            reader.OnMessageReady += HandleMessage;
        }


        private void HandleMessage(byte[] arg1, int arg2, int arg3)
        {
            base.HandleReceived(arg1, arg2, arg3);
        }


        protected override void HandleReceived(byte[] buffer, int offset, int count)
        {
            reader.ParseBytes(buffer, offset, count);
        }


        protected override IMessageQueue CreateMessageQueue()
        {
            if (UseQueue)
                return new MessageQueue<DelimitedMessageWriter>(MaxIndexedMemory, new DelimitedMessageWriter());

            return new MessageBuffer(MaxIndexedMemory);
        }
    }
}