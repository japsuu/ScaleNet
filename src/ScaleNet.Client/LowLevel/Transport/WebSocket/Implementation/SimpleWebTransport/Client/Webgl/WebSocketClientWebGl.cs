using System;
using System.Collections.Generic;
using ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Client.Webgl
{
    public class WebSocketClientWebGl : SimpleWebClient
    {
        private static readonly Dictionary<int, WebSocketClientWebGl> Instances = new();

        /// <summary>
        /// key for instances sent between c# and js
        /// </summary>
        private int _index;

        /// <summary>
        /// Message sent by high level while still connecting, they will be send after onOpen is called
        /// <para>this is a workaround for mirage where send is called right after Connect</para>
        /// </summary>
        private Queue<byte[]>? _connectingSendQueue;


        internal WebSocketClientWebGl(int maxMessageSize, int maxMessagesPerTick) : base(maxMessageSize, maxMessagesPerTick)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            throw new NotSupportedException();
#endif
        }


        public bool CheckJsConnected() => SimpleWebJsLib.IsConnected(_index);


        public override void Connect(Uri serverAddress)
        {
            _index = SimpleWebJsLib.Connect(serverAddress.ToString(), OpenCallback, CloseCallBack, MessageCallback, ErrorCallback);
            Instances.Add(_index, this);
            State = ConnectionState.Connecting;
        }


        public override void Disconnect()
        {
            State = ConnectionState.Disconnecting;

            // disconnect should cause closeCallback and OnDisconnect to be called
            SimpleWebJsLib.Disconnect(_index);
        }


        public override void Send(byte[] data, int offset, int length)
        {
            if (length > MaxMessageSize)
            {
                SimpleWebLog.Error($"Cant send message with length {length} because it is over the max size of {MaxMessageSize}");
                return;
            }

            if (State == ConnectionState.Connected)
                SimpleWebJsLib.Send(_index, data, offset, length);
            else
            {
                if (_connectingSendQueue == null)
                    _connectingSendQueue = new Queue<byte[]>();
                _connectingSendQueue.Enqueue(data.AsSpan(offset, length).ToArray());
            }
        }


        private void OnOpen()
        {
            ReceiveQueue.Enqueue(new Message(EventType.Connected));
            State = ConnectionState.Connected;

            if (_connectingSendQueue != null)
            {
                while (_connectingSendQueue.Count > 0)
                {
                    byte[] next = _connectingSendQueue.Dequeue();
                    SimpleWebJsLib.Send(_index, next, 0, next.Length);
                }

                _connectingSendQueue = null;
            }
        }


        private void OnClose()
        {
            // this code should be last in this class

            ReceiveQueue.Enqueue(new Message(EventType.Disconnected));
            State = ConnectionState.Disconnected;
            Instances.Remove(_index);
        }


        private void OnMessage(IntPtr bufferPtr, int count)
        {
            try
            {
                ArrayBuffer buffer = BufferPool.Take(count);
                buffer.CopyFrom(bufferPtr, count);

                ReceiveQueue.Enqueue(new Message(buffer));
            }
            catch (Exception e)
            {
                SimpleWebLog.Error($"onData {e.GetType()}: {e.Message}\n{e.StackTrace}");
                ReceiveQueue.Enqueue(new Message(e));
            }
        }


        private void OnErr()
        {
            ReceiveQueue.Enqueue(new Message(new Exception("Javascript Websocket error")));
            Disconnect();
        }


#if UNITY_WEBGL
        [MonoPInvokeCallback(typeof(Action<int>))]
#endif
        private static void OpenCallback(int index) => Instances[index].OnOpen();

#if UNITY_WEBGL
        [MonoPInvokeCallback(typeof(Action<int>))]
#endif
        private static void CloseCallBack(int index) => Instances[index].OnClose();

#if UNITY_WEBGL
        [MonoPInvokeCallback(typeof(Action<int, IntPtr, int>))]
#endif
        private static void MessageCallback(int index, IntPtr bufferPtr, int count) => Instances[index].OnMessage(bufferPtr, count);

#if UNITY_WEBGL
        [MonoPInvokeCallback(typeof(Action<int>))]
#endif
        private static void ErrorCallback(int index) => Instances[index].OnErr();
    }
}