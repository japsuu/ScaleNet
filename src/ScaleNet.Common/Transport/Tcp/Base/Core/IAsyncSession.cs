using System;
using System.Net;
using ScaleNet.Common.Transport.Components.Statistics;

namespace ScaleNet.Common.Transport.Tcp.Base.Core
{
    internal interface IAsyncSession : IDisposable
    {
        /// <summary>
        /// Remote endpoint of this session
        /// </summary>
        IPEndPoint RemoteEndpoint { get; }

        event Action<Guid, byte[], int, int>? BytesReceived;

        /// <summary>
        /// Called when session is closed.
        /// </summary>
        event Action<Guid>? SessionClosed;


        /// <summary>
        /// Sends buffer asynchronously.
        /// </summary>
        void SendAsync(byte[] buffer);


        /// <summary>
        /// Sends a buffer region asynchronously
        /// </summary>
        void SendAsync(byte[] buffer, int offset, int count);


        /// <summary>
        /// Starts the session
        /// </summary>
        void StartSession();


        /// <summary>
        /// Disconnects the client and disposes the resources.
        /// </summary>
        void EndSession();


        /// <summary>
        /// gets the session statistics.
        /// </summary>
        /// <returns></returns>
        SessionStatistics GetSessionStatistics();
    }
}