using System;
using System.Net.Security;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageBuffer;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageBuffer.Interface;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.MessageProcessor.Unmanaged;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.SSL.ByteMessage
{
    internal class SslByteMessageSession : SslSession
    {
        private readonly ByteMessageReader _reader;


        public SslByteMessageSession(Guid sessionId, SslStream sessionStream) : base(sessionId, sessionStream)
        {
            _reader = new ByteMessageReader();
            _reader.OnMessageReady += HandleMessage;
        }


        private void HandleMessage(byte[] arg1, int arg2, int arg3)
        {
            base.HandleReceived(arg1, arg2, arg3);
        }


        protected override void HandleReceived(byte[] buffer, int offset, int count)
        {
            _reader.ParseBytes(buffer, offset, count);
        }


        protected override IMessageQueue CreateMessageQueue()
        {
            if (UseQueue)
                return new MessageQueue<DelimitedMessageWriter>(MaxIndexedMemory, new DelimitedMessageWriter());

            return new MessageBuffer(MaxIndexedMemory);
        }
    }
}