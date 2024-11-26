using System;
using System.Threading.Tasks;
using ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Components.Statistics;

namespace ScaleNet.Common.Transport.TCP.StandardNetworkLibrary.Base.Core
{
    public abstract class TcpClientBase
    {
        /// <summary>
        ///     Callback delegate of bytes received
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public delegate void BytesReceived(byte[] bytes, int offset, int count);

        /// <summary>
        ///     <br /> Determines whether to use a queue or a buffer for the message gathering mechanism.
        ///     <br /><br /> UseQueue requires your byte[] sources to not be modified after send because your data may be copied
        ///     asynchronously.
        ///     <br /><br /> UseBuffer will copy your data into a buffer on caller thread. Socket will perform buffer swaps.
        ///     You can modify or reuse your data safely.
        /// </summary>
        public ScatterGatherConfig GatherConfig = ScatterGatherConfig.UseQueue;

        /// <summary>
        ///     Fires when the client is connected;
        /// </summary>
        public Action? Connected { get; set; }

        /// <summary>
        ///     Fires when connection is failed when the connection is initiated with <see cref="ConnectAsync(string, int)" />
        /// </summary>
        public Action<Exception>? ConnectFailed { get; set; }

        /// <summary>
        ///     Fires when the client is disconnected.
        /// </summary>
        public Action? Disconnected { get; set; }

        /// <summary>
        ///     Invoked when bytes are received. New receive operation will not be performed until this callback is finalized.
        ///     <br /><br />Callback data is region of the socket buffer.
        ///     <br />Do a copy if you intend to store the data or use it on different thread.
        /// </summary>
        public BytesReceived? OnBytesReceived { get; set; }

        /// <summary>
        ///     Maximum amount of indexed memory to be held inside the message queue.
        ///     It is the cumulative message lengths that are queued.
        /// </summary>
        public int MaxIndexedMemory { get; set; } = 128000000;

        /// <summary>
        ///     Is Client connecting
        /// </summary>
        public bool IsConnecting { get; internal set; }

        /// <summary>
        ///     Is client successfully connected.
        /// </summary>
        public bool IsConnected { get; internal set; }


        /// <summary>
        ///     Connect synchronously
        /// </summary>
        /// <param name="IP"></param>
        /// <param name="port"></param>
        public abstract void Connect(string IP, int port);


        /// <summary>
        ///     Connects asynchronously and notifies the results from either <see cref="Connected" /> or
        ///     <see cref="ConnectFailed" />
        /// </summary>
        /// <param name="IP"></param>
        /// <param name="port"></param>
        public abstract void ConnectAsync(string IP, int port);


        /// <summary>
        ///     Connects asynchronously with an awaitable task.
        /// </summary>
        /// <param name="IP"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public abstract Task<bool> ConnectAsyncAwaitable(string IP, int port);


        /// <summary>
        ///     Sends the message asynchronously or enqueues a message
        /// </summary>
        /// <param name="buffer"></param>
        public abstract void SendAsync(byte[] buffer);


        /// <summary>
        ///     Sends or enqueues message asynchronously
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public abstract void SendAsync(byte[] buffer, int offset, int count);


        /// <summary>
        ///     Disconnects the client.
        /// </summary>
        public abstract void Disconnect();


        /// <summary>
        ///     Gets session statistics.
        /// </summary>
        /// <param name="generalStats"></param>
        public abstract void GetStatistics(out TcpStatistics generalStats);
    }
}