using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using ScaleNet.Common.Transport.Tcp.Base.Core;

namespace ScaleNet.Common.Transport.Tcp.SSL.ByteMessage
{
    public class SslByteMessageClient : SslClient
    {
        public SslByteMessageClient(X509Certificate2 certificate) : base(certificate)
        {
        }


        private protected override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            SslByteMessageSession ses = new(guid, tuple.Item1);
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.RemoteEndpoint = tuple.Item2;
            if (GatherConfig == ScatterGatherConfig.UseQueue)
                ses.UseQueue = true;
            else
                ses.UseQueue = false;

            return ses;
        }
    }
}