﻿using NetworkLibrary.TCP.Base;
using NetworkLibrary.TCP.SSL.Base;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NetworkLibrary.TCP.SSL.ByteMessage
{
    public class SslByteMessageClient : SslClient
    {

        public SslByteMessageClient(X509Certificate2 certificate) : base(certificate)
        {
        }

        public SslByteMessageClient() : base()
        {
        }


        private protected override IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            var ses = new SslByteMessageSession(guid, tuple.Item1);
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
