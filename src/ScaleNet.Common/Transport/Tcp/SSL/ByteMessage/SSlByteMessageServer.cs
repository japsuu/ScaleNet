using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using ScaleNet.Common.Transport.Tcp.Base.Core;

namespace ScaleNet.Common.Transport.Tcp.SSL.ByteMessage
{
    public class SslByteMessageServer : SslServer
    {
        public SslByteMessageServer(int port, X509Certificate2 certificate) : base(port, certificate)
        {
        }


        private protected override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            SslByteMessageSession ses = new(guid, tuple.Item1);
            ses.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            ses.RemoteEndpoint = tuple.Item2;

            ses.UseQueue = GatherConfig == ScatterGatherConfig.UseQueue;

            return ses;
        }
    }
}