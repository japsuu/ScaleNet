using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Base.Core;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.Statistics;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.SSL
{
    /// <summary>
    ///     Standard SSL client
    /// </summary>
    public class SslClient : TcpClientBase, IDisposable
    {
        private readonly X509Certificate2 _certificate;
        private protected IAsyncSession? ClientSession;
        protected Socket? ClientSocket;
        protected SslStream? SslStream;
        private TcpClientStatisticsPublisher? _statisticsPublisher;

        /// <summary>
        ///     Assign if you need to validate certificates. By default all certificates are accepted.
        /// </summary>
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;



        /// <summary>
        ///     initializes new instance with given certificate
        /// </summary>
        /// <param name="certificate"></param>
        public SslClient(X509Certificate2 certificate)
        {
            _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
            RemoteCertificateValidationCallback += DefaultValidationCallbackHandler;
        }


        public virtual void Dispose()
        {
            ClientSession?.EndSession();
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
        public override void SendAsync(byte[] bytes)
        {
            ClientSession?.SendAsync(bytes);
        }


        /// <summary>
        ///     Sends a message without blocking
        ///     <br />If ScatterGatherConfig.UseQueue is selected message will be copied to single buffer before added into queue.
        ///     <br />If ScatterGatherConfig.UseBuffer message will be copied to message buffer on caller thread,
        ///     <br /> <br />ScatterGatherConfig.UseBuffer is the recommended configuration if your sends are buffer region
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void SendAsync(byte[] buffer, int offset, int count)
        {
            ClientSession?.SendAsync(buffer, offset, count);
        }


        public override void Disconnect()
        {
            ClientSession?.EndSession();
        }


        public override void GetStatistics(out TcpStatistics generalStats)
        {
            if (_statisticsPublisher == null)
                throw new InvalidOperationException("Client is not connected");
            
            _statisticsPublisher.GetStatistics(out generalStats);
        }


#region Connect

        public override void Connect(string ip, int port)
        {
            try
            {
                IsConnecting = true;
                Socket clientSocket = GetSocket();

                clientSocket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
                OnConnected(ip, clientSocket);
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

                TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

                SocketAsyncEventArgs args = new();
                args.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                args.Completed += (_, arg) => { HandleResult(arg); };

                if (!clientSocket.ConnectAsync(args))
                    HandleResult(args);

                void HandleResult(SocketAsyncEventArgs arg)
                {
                    if (arg.SocketError == SocketError.Success)
                    {
                        OnConnected(ip, clientSocket);
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
                    bool result;
                    try
                    {
                        result = await ConnectAsyncAwaitable(IP, port).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        ConnectFailed?.Invoke(ex);
                        return;
                    }
                    finally
                    {
                        IsConnecting = false;
                    }

                    if (result)
                        Connected?.Invoke();
                });
        }


        private void OnConnected(string domainName, Socket clientSocket)
        {
            SslStream = new SslStream(new NetworkStream(clientSocket, true), false, ValidateCeriticate);
            SslStream.AuthenticateAsClient(
                domainName,
                new X509CertificateCollection(new X509Certificate[] { _certificate }), SslProtocols.None, true);

            ClientSocket = clientSocket;
            Guid id = Guid.NewGuid();

            ClientSession = CreateSession(id, new ValueTuple<SslStream, IPEndPoint>(SslStream, (IPEndPoint)clientSocket.RemoteEndPoint));
            ClientSession.SessionClosed += _ => Disconnected?.Invoke();
            ClientSession.BytesReceived += HandleBytesReceived;
            ClientSession.StartSession();

            _statisticsPublisher = new TcpClientStatisticsPublisher(ClientSession);
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