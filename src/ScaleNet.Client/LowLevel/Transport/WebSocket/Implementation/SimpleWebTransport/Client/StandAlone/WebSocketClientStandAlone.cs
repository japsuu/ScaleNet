using System;
using System.Net.Sockets;
using System.Threading;
using ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Client.StandAlone
{
    public class WebSocketClientStandAlone : SimpleWebClient
    {
        private readonly ClientSslHelper _sslHelper;
        private readonly ClientHandshake _handshake;
        private readonly TcpConfig _tcpConfig;
        private Connection? _conn;


        internal WebSocketClientStandAlone(int maxMessageSize, int maxMessagesPerTick, TcpConfig tcpConfig, ClientSslContext? sslContext) : base(maxMessageSize, maxMessagesPerTick)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new NotSupportedException();
#else
            _sslHelper = new ClientSslHelper(sslContext);
            _handshake = new ClientHandshake();
            _tcpConfig = tcpConfig;
#endif
        }


        public override void Connect(Uri serverAddress)
        {
            State = ConnectionState.Connecting;

            // create connection here before thread so that send queue exist for MiragePeer to send to
            TcpClient client = new();
            TcpConfig.Apply(_tcpConfig, client);

            // create connection object here so dispose correctly disconnects on failed connect
            _conn = new Connection(client, AfterConnectionDisposed);

            Thread receiveThread = new(() => ConnectAndReceiveLoop(serverAddress));
            receiveThread.IsBackground = true;
            receiveThread.Start();
        }


        private void ConnectAndReceiveLoop(Uri serverAddress)
        {
            try
            {
                // connection created above
                TcpClient client = _conn!.Client!;

                //// create connection object here so dispose correctly disconnects on failed connect
                //conn = new Connection(client, AfterConnectionDisposed);
                _conn.ReceiveThread = Thread.CurrentThread;

                try
                {
                    client.Connect(serverAddress.Host, serverAddress.Port);
                }
                catch (SocketException)
                {
                    client.Dispose();
                    throw;
                }


                bool success = _sslHelper.TryCreateStream(_conn, serverAddress);
                if (!success)
                {
                    SimpleWebLog.Warn("Failed to create Stream");
                    _conn.Dispose();
                    return;
                }

                success = ClientHandshake.TryHandshake(_conn, serverAddress);
                if (!success)
                {
                    SimpleWebLog.Warn("Failed Handshake");
                    _conn.Dispose();
                    return;
                }

                SimpleWebLog.Info("HandShake Successful");

                State = ConnectionState.Connected;

                ReceiveQueue.Enqueue(new Message(EventType.Connected));

                Thread sendThread = new(
                    () =>
                    {
                        SendLoop.Config sendConfig = new(
                            _conn,
                            Constants.HEADER_SIZE + Constants.MASK_SIZE + MaxMessageSize,
                            true);

                        SendLoop.Loop(sendConfig);
                    });

                _conn.SendThread = sendThread;
                sendThread.IsBackground = true;
                sendThread.Start();

                ReceiveLoop.Config config = new(
                    _conn,
                    MaxMessageSize,
                    false,
                    ReceiveQueue,
                    BufferPool);
                ReceiveLoop.Loop(config);
            }
            catch (ThreadInterruptedException e)
            {
                SimpleWebLog.InfoException(e);
            }
            catch (ThreadAbortException e)
            {
                SimpleWebLog.InfoException(e);
            }
            catch (Exception e)
            {
                SimpleWebLog.Exception(e);
            }
            finally
            {
                // close here incase connect fails
                _conn?.Dispose();
            }
        }


        private void AfterConnectionDisposed(Connection conn)
        {
            State = ConnectionState.Disconnected;

            // make sure Disconnected event is only called once
            ReceiveQueue.Enqueue(new Message(EventType.Disconnected));
        }


        public override void Disconnect()
        {
            State = ConnectionState.Disconnecting;
            SimpleWebLog.Info("Disconnect Called");
            if (_conn == null)
                State = ConnectionState.Disconnected;
            else
                _conn?.Dispose();
        }


        public override void Send(byte[] data, int offset, int length)
        {
            ArrayBuffer buffer = BufferPool.Take(length);
            buffer.CopyFrom(data, offset, length);

            _conn!.SendQueue.Enqueue(buffer);
            _conn.SendPending.Set();
        }
    }
}