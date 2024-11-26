using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using ScaleNet.Common.Transport.Components.Statistics;
using ScaleNet.Common.Transport.Tcp.Base.Core;

namespace ScaleNet.Common.Transport.Tcp.SSL
{
    /// <summary>
    ///     Standard SSL client
    /// </summary>
    public class SslClient : TcpClientBase, IDisposable
    {
        private readonly X509Certificate2 certificate;
        private protected IAsyncSession clientSession;
        protected Socket clientSocket;

        /// <summary>
        ///     Assign if you need to validate certificates. By default all certificates are accepted.
        /// </summary>
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;

        protected SslStream sslStream;
        private TcpClientStatisticsPublisher statisticsPublisher;


        /// <summary>
        ///     initialises new instace with given certificate
        /// </summary>
        /// <param name="certificate"></param>
        public SslClient(X509Certificate2 certificate)
        {
            this.certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
            RemoteCertificateValidationCallback += DefaultValidationCallbackHandler;
        }


        public virtual void Dispose()
        {
            clientSession?.EndSession();
        }


        private Socket GetSocket()
        {
            Socket socket = new(SocketType.Stream, ProtocolType.Tcp);
            return socket;
        }


        private protected virtual IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            SslSession ses = new(guid, tuple.Item1);
            ses.MaxIndexedMemory = MaxIndexedMemory;
            ses.RemoteEndpoint = tuple.Item2;

            if (GatherConfig == ScatterGatherConfig.UseQueue)
                ses.UseQueue = true;
            else
                ses.UseQueue = false;

            return ses;
        }


        protected virtual void HandleBytesReceived(Guid sesonId, byte[] bytes, int offset, int count)
        {
            OnBytesReceived?.Invoke(bytes, offset, count);
        }


        /// <summary>
        ///     Sends a message without blocking.
        ///     <br />If ScatterGatherConfig.UseQueue is selected message will be added to queue without copy.
        ///     <br />If ScatterGatherConfig.UseBuffer message will be copied to message buffer on caller thread.
        /// </summary>
        /// <param name="buffer"></param>
        public override void SendAsync(byte[] bytes)
        {
            clientSession.SendAsync(bytes);
        }


        /// <summary>
        ///     Sends a message without blocking
        ///     <br />If ScatterGatherConfig.UseQueue is selected message will be copied to single buffer before added into queue.
        ///     <br />If ScatterGatherConfig.UseBuffer message will be copied to message buffer on caller thread,
        ///     <br /> <br />ScatterGatherConfig.UseBuffer is the reccomeded configuration if your sends are buffer region
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void SendAsync(byte[] buffer, int offset, int count)
        {
            clientSession.SendAsync(buffer, offset, count);
        }


        public override void Disconnect()
        {
            clientSession?.EndSession();
        }


        public override void GetStatistics(out TcpStatistics generalStats)
        {
            statisticsPublisher.GetStatistics(out generalStats);
        }


#region Connect

        public override void Connect(string ip, int port)
        {
            try
            {
                IsConnecting = true;
                Socket clientSocket = GetSocket();

                clientSocket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
                Connected(ip, clientSocket);
            }
            finally
            {
                IsConnecting = false;
            }
        }


        public override Task<bool> ConnectAsyncAwaitable(string ip, int port)
        {
            try
            {
                IsConnecting = true;
                Socket clientSocket = GetSocket();

                // this shit is terrible..

                TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

                SocketAsyncEventArgs earg = new();
                earg.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                earg.Completed += (ignored, arg) => { HandleResult(arg); };

                if (!clientSocket.ConnectAsync(earg))
                    HandleResult(earg);

                void HandleResult(SocketAsyncEventArgs arg)
                {
                    if (arg.SocketError == SocketError.Success)
                    {
                        Connected(ip, clientSocket);
                        tcs.SetResult(true);
                    }
                    else
                        tcs.TrySetException(new SocketException((int)arg.SocketError));
                }

                return tcs.Task;
            }
            finally
            {
                IsConnecting = false;
            }
        }


        public override void ConnectAsync(string IP, int port)
        {
            Task.Run(
                async () =>
                {
                    IsConnecting = true;
                    bool result = false;
                    try
                    {
                        result = await ConnectAsyncAwaitable(IP, port).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        OnConnectFailed?.Invoke(ex);
                        return;
                    }
                    finally
                    {
                        IsConnecting = false;
                    }

                    if (result)
                        OnConnected?.Invoke();
                });
        }


        private void Connected(string domainName, Socket clientSocket)
        {
            sslStream = new SslStream(new NetworkStream(clientSocket, true), false, ValidateCeriticate);
            sslStream.AuthenticateAsClient(
                domainName,
                new X509CertificateCollection(new[] { certificate }), SslProtocols.None, true);

            this.clientSocket = clientSocket;
            Guid Id = Guid.NewGuid();

            clientSession = CreateSession(Id, new ValueTuple<SslStream, IPEndPoint>(sslStream, (IPEndPoint)clientSocket.RemoteEndPoint));
            clientSession.OnSessionClosed += id => OnDisconnected?.Invoke();
            clientSession.OnBytesRecieved += HandleBytesReceived;
            clientSession.StartSession();

            statisticsPublisher = new TcpClientStatisticsPublisher(clientSession);
            IsConnecting = false;
            IsConnected = true;
        }

#endregion Connect


#region Validate

        protected virtual bool ValidateCeriticate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
            RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);


        private bool DefaultValidationCallbackHandler(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            //return true;
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            return false;
        }

#endregion Validate
    }
}