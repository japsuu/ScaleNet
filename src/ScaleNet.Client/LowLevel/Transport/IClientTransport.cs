﻿using System;
using ScaleNet.Common;

namespace ScaleNet.Client.LowLevel.Transport
{
    public interface IClientTransport : IDisposable
    {
        public string Address { get; }
        public ushort Port { get; }
        public ConnectionState State { get; }
    
        public bool ConnectClient();
        public bool ReconnectClient();
        public bool DisconnectClient();
        
        public void IterateIncoming();
        public void IterateOutgoing();

        /// <summary>
        /// Sends the given message to the server asynchronously.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void SendAsync<T>(T message) where T : INetMessage;
    
        /// <summary>
        /// Called after the local client connection state changes.
        /// </summary>
        public event Action<ConnectionStateArgs>? ConnectionStateChanged;
    
        /// <summary>
        /// Called to handle incoming messages.<br/>
        /// Implementations are required to be thread-safe, as this event may be raised from multiple threads.
        /// </summary>
        public event Action<DeserializedNetMessage>? MessageReceived;
    }
}