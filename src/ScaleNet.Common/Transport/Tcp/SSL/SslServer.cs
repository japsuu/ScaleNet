using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using ScaleNet.Common.Transport.Components.Statistics;
using ScaleNet.Common.Transport.Tcp.Base.Core;
using ScaleNet.Common.Transport.Utils;

namespace ScaleNet.Common.Transport.Tcp.SSL
{
    public class SslServer : TcpServerBase
    {
        private readonly X509Certificate2 certificate;
        private readonly TcpServerStatisticsPublisher statisticsPublisher;

        /// <summary>
        ///     Invoked when bytes are received. New receive operation will not be performed until this callback is finalised.
        ///     <br /><br />Callback data is region of the socket buffer.
        ///     <br />Do a copy if you intend to store the data or use it on different thread.
        /// </summary>
        public BytesRecieved OnBytesReceived;

        /// <summary>
        ///     Invoked when client is connected to server.
        /// </summary>
        public ClientAccepted OnClientAccepted;

        /// <summary>
        ///     Invoked when client is disconnected
        /// </summary>
        public ClientDisconnected OnClientDisconnected;

        public ClientConnectionRequest OnClientRequestedConnection;

        /// <summary>
        ///     Assign if you need to validate certificates. By default all certificates are accepted.
        /// </summary>
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback;

        private Socket serverSocket;

        private protected ConcurrentDictionary<Guid, IAsyncSession> Sessions = new();


        public SslServer(int port, X509Certificate2 certificate)
        {
            ServerPort = port;
            this.certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
            OnClientRequestedConnection = socket => true;
            RemoteCertificateValidationCallback += DefaultValidationCallback;

            statisticsPublisher = new TcpServerStatisticsPublisher(Sessions);
        }


        public bool Stopping { get; private set; }

        public int SessionCount => Sessions.Count;
        internal ConcurrentDictionary<Guid, TcpStatistics> Stats { get; } = new();


        public override void StartServer()
        {
            serverSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            serverSocket.ReceiveBufferSize = ServerSockerReceiveBufferSize;
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, ServerPort));

            serverSocket.Listen(10000);

            // serverSocket.BeginAccept(Accepted, null);
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                SocketAsyncEventArgs e = new();
                e.Completed += Accepted;
                if (!serverSocket.AcceptAsync(e))
                    ThreadPool.UnsafeQueueUserWorkItem(s => Accepted(null, e), null);
            }
        }


        private void Accepted(object sender, SocketAsyncEventArgs acceptedArg)
        {
            if (Stopping)
                return;

            SocketAsyncEventArgs nextClient = new();
            nextClient.Completed += Accepted;

            if (!serverSocket.AcceptAsync(nextClient))
                ThreadPool.UnsafeQueueUserWorkItem(s => Accepted(null, nextClient), null);

            if (acceptedArg.SocketError != SocketError.Success)
            {
                TransportLogger.Log(
                    TransportLogger.LogLevel.Error, "While Accepting Client an Error Occured:" + Enum.GetName(typeof(SocketError), acceptedArg.SocketError));
                return;
            }

            if (!ValidateConnection(acceptedArg.AcceptSocket))
                return;

            SslStream sslStream = new(new NetworkStream(acceptedArg.AcceptSocket, true), false, ValidateCeriticate);
            try
            {
                Authenticate(
                    (IPEndPoint)acceptedArg.AcceptSocket.RemoteEndPoint, sslStream,
                    certificate, true, SslProtocols.None, false);
            }
            catch (Exception ex)
                when (ex is AuthenticationException || ex is ObjectDisposedException)
            {
                TransportLogger.Log(TransportLogger.LogLevel.Error, "Athentication as server failed: " + ex.Message);
            }

            acceptedArg.Dispose();
        }


        private async void Authenticate(IPEndPoint remoteEndPoint, SslStream sslStream, X509Certificate2 certificate, bool v1, SslProtocols none, bool v2)
        {
            Task task = sslStream.AuthenticateAsServerAsync(certificate, v1, none, v2);
            if (await Task.WhenAny(task, Task.Delay(10000)).ConfigureAwait(false) == task)
            {
                try
                {
                    //await task;
                    Guid sessionId = Guid.NewGuid();
                    IAsyncSession ses = CreateSession(sessionId, (sslStream, remoteEndPoint));
                    ses.OnBytesRecieved += HandleBytesReceived;
                    ses.OnSessionClosed += HandeDeadSession;
                    Sessions.TryAdd(sessionId, ses);
                    ses.StartSession();

                    OnClientAccepted?.Invoke(sessionId);
                }
                catch (Exception ex)
                {
                    TransportLogger.Log(TransportLogger.LogLevel.Error, "Athentication as server failed: " + ex.Message);
                    sslStream.Close();
                    sslStream.Dispose();
                }
            }
            else
            {
                TransportLogger.Log(TransportLogger.LogLevel.Error, "Athentication as server timed out: ");
                sslStream.Close();
                sslStream.Dispose();
            }
        }


        protected virtual bool ValidateConnection(Socket clientsocket) => OnClientRequestedConnection.Invoke(clientsocket);


        private bool ValidateCeriticate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
            RemoteCertificateValidationCallback.Invoke(sender, certificate, chain, sslPolicyErrors);


        private bool DefaultValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            return false;
        }


        private void HandeDeadSession(Guid id)
        {
            OnClientDisconnected?.Invoke(id);
            Sessions.TryRemove(id, out _);
        }


        private protected virtual IAsyncSession CreateSession(Guid guid, ValueTuple<SslStream, IPEndPoint> tuple)
        {
            SslSession ses = new(guid, tuple.Item1);
            ses.MaxIndexedMemory = MaxIndexedMemoryPerClient;
            ses.DropOnCongestion = DropOnBackPressure;
            ses.RemoteEndpoint = tuple.Item2;

            if (GatherConfig == ScatterGatherConfig.UseQueue)
                ses.UseQueue = true;
            else
                ses.UseQueue = false;

            return ses;
        }


        protected virtual void HandleBytesReceived(Guid arg1, byte[] arg2, int arg3, int arg4)
        {
            OnBytesReceived?.Invoke(arg1, arg2, arg3, arg4);
        }


        public override void SendBytesToClient(Guid clientId, byte[] bytes)
        {
            if (Sessions.TryGetValue(clientId, out IAsyncSession? session))
                session.SendAsync(bytes);
        }


        public void SendBytesToClient(Guid clientId, byte[] bytes, int offset, int count)
        {
            if (Sessions.TryGetValue(clientId, out IAsyncSession? session))
                session.SendAsync(bytes, offset, count);
        }


        public override void SendBytesToAllClients(byte[] bytes)
        {
            foreach (KeyValuePair<Guid, IAsyncSession> session in Sessions)
                session.Value.SendAsync(bytes);
        }


        public override void ShutdownServer()
        {
            Stopping = true;
            serverSocket.Close();
            serverSocket.Dispose();
            foreach (KeyValuePair<Guid, IAsyncSession> item in Sessions)
                item.Value.EndSession();
            Sessions.Clear();
        }


        public override void CloseSession(Guid sessionId)
        {
            if (Sessions.TryGetValue(sessionId, out IAsyncSession? session))
                session.EndSession();
        }


        public override void GetStatistics(out TcpStatistics generalStats, out ConcurrentDictionary<Guid, TcpStatistics> sessionStats)
        {
            statisticsPublisher.GetStatistics(out generalStats, out sessionStats);
        }


        public override IPEndPoint GetSessionEndpoint(Guid sessionId)
        {
            if (Sessions.TryGetValue(sessionId, out IAsyncSession? session))
                return session.RemoteEndpoint;

            return null;
        }
    }
}