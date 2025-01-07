using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace ScaleNet.Server.LowLevel.Transport.Tcp;

/// <summary>
/// SSL server is used to connect, disconnect and manage SSL sessions
/// </summary>
/// <remarks>Thread-safe</remarks>
public class SslServer : IDisposable
{
    // Server acceptor
    private Socket? _acceptorSocket;
    private SocketAsyncEventArgs? _acceptorEventArg;

    /// <summary>
    /// Server sessions
    /// </summary>
    protected readonly ConcurrentDictionary<Guid, SslSession> Sessions = new();

    // Server statistic
    internal long BytesPendingStat;
    internal long BytesSentStat;
    internal long BytesReceivedStat;
    
    /// <summary>
    /// Server Id
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// SSL server address
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// SSL server port
    /// </summary>
    public ushort Port { get; }

    /// <summary>
    /// SSL context
    /// </summary>
    public ServerSslContext Context { get; }

    /// <summary>
    /// Endpoint
    /// </summary>
    public EndPoint Endpoint { get; private set; }
    
    /// <summary>
    /// Is the server started?
    /// </summary>
    public bool IsStarted { get; private set; }

    /// <summary>
    /// Is the server accepting new clients?
    /// </summary>
    public bool IsAccepting { get; private set; }

    /// <summary>
    /// Number of sessions connected to the server
    /// </summary>
    public long ConnectedSessions => Sessions.Count;

    /// <summary>
    /// Number of bytes pending sent by the server
    /// </summary>
    public long BytesPending => BytesPendingStat;

    /// <summary>
    /// Number of bytes sent by the server
    /// </summary>
    public long BytesSent => BytesSentStat;

    /// <summary>
    /// Number of bytes received by the server
    /// </summary>
    public long BytesReceived => BytesReceivedStat;

    /// <summary>
    /// Option: acceptor backlog size
    /// </summary>
    /// <remarks>
    /// This option will set the listening socket's backlog size
    /// </remarks>
    public int OptionAcceptorBacklog { get; set; } = 1024;

    /// <summary>
    /// Option: dual mode socket
    /// </summary>
    /// <remarks>
    /// Specifies whether the Socket is a dual-mode socket used for both IPv4 and IPv6.
    /// Will work only if socket is bound on IPv6 address.
    /// </remarks>
    public bool OptionDualMode { get; set; }

    /// <summary>
    /// Option: keep alive
    /// </summary>
    /// <remarks>
    /// This option will setup SO_KEEPALIVE if the OS support this feature
    /// </remarks>
    public bool OptionKeepAlive { get; set; }

    /// <summary>
    /// Option: TCP keep alive time
    /// </summary>
    /// <remarks>
    /// The number of seconds a TCP connection will remain alive/idle before keepalive probes are sent to the remote
    /// </remarks>
    public int OptionTcpKeepAliveTime { get; set; } = -1;

    /// <summary>
    /// Option: TCP keep alive interval
    /// </summary>
    /// <remarks>
    /// The number of seconds a TCP connection will wait for a keepalive response before sending another keepalive probe
    /// </remarks>
    public int OptionTcpKeepAliveInterval { get; set; } = -1;

    /// <summary>
    /// Option: TCP keep alive retry count
    /// </summary>
    /// <remarks>
    /// The number of TCP keep alive probes that will be sent before the connection is terminated
    /// </remarks>
    public int OptionTcpKeepAliveRetryCount { get; set; } = -1;

    /// <summary>
    /// Option: no delay
    /// </summary>
    /// <remarks>
    /// This option will enable/disable Nagle's algorithm for SSL protocol
    /// </remarks>
    public bool OptionNoDelay { get; set; }

    /// <summary>
    /// Option: reuse address
    /// </summary>
    /// <remarks>
    /// This option will enable/disable SO_REUSEADDR if the OS support this feature
    /// </remarks>
    public bool OptionReuseAddress { get; set; }

    /// <summary>
    /// Option: enables a socket to be bound for exclusive access
    /// </summary>
    /// <remarks>
    /// This option will enable/disable SO_EXCLUSIVEADDRUSE if the OS support this feature
    /// </remarks>
    public bool OptionExclusiveAddressUse { get; set; }

    /// <summary>
    /// Option: receive buffer size
    /// </summary>
    public int OptionReceiveBufferSize { get; set; } = 8192;

    /// <summary>
    /// Option: send buffer size
    /// </summary>
    public int OptionSendBufferSize { get; set; } = 8192;

    
    /// <summary>
    /// Initialize SSL server with a given IP address and port number
    /// </summary>
    /// <param name="context">SSL context</param>
    /// <param name="address">IP address</param>
    /// <param name="port">Port number</param>
    public SslServer(ServerSslContext context, IPAddress address, int port) : this(context, new IPEndPoint(address, port))
    {
    }


    /// <summary>
    /// Initialize SSL server with a given IP endpoint
    /// </summary>
    /// <param name="context">SSL context</param>
    /// <param name="endpoint">IP endpoint</param>
    public SslServer(ServerSslContext context, IPEndPoint endpoint) : this(context, endpoint, endpoint.Address.ToString(), (ushort)endpoint.Port)
    {
    }


    /// <summary>
    /// Initialize SSL server with a given SSL context, endpoint, address and port
    /// </summary>
    /// <param name="context">SSL context</param>
    /// <param name="endpoint">Endpoint</param>
    /// <param name="address">Server address</param>
    /// <param name="port">Server port</param>
    private SslServer(ServerSslContext context, EndPoint endpoint, string address, ushort port)
    {
        Id = Guid.NewGuid();
        Address = address;
        Port = port;
        Context = context;
        Endpoint = endpoint;
    }
    

#region Start/Stop server
    
    /// <summary>
    /// Create a new socket object
    /// </summary>
    /// <remarks>
    /// Method may be overridden if you need to prepare some specific socket object in your implementation.
    /// </remarks>
    /// <returns>Socket object</returns>
    protected virtual Socket CreateSocket() => new(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);


    /// <summary>
    /// Start the server
    /// </summary>
    /// <returns>'true' if the server was successfully started, 'false' if the server failed to start</returns>
    public virtual bool Start()
    {
        Debug.Assert(!IsStarted, "SSL server is already started!");
        if (IsStarted)
            return false;

        // Setup acceptor event arg
        _acceptorEventArg = new SocketAsyncEventArgs();
        _acceptorEventArg.Completed += OnSocketAcceptorCompletedAsync;

        // Create a new acceptor socket
        _acceptorSocket = CreateSocket();

        // Update the acceptor socket disposed flag
        IsSocketDisposed = false;

        // Apply the option: reuse address
        _acceptorSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, OptionReuseAddress);

        // Apply the option: exclusive address use
        _acceptorSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, OptionExclusiveAddressUse);

        // Apply the option: dual mode (this option must be applied before listening)
        if (_acceptorSocket.AddressFamily == AddressFamily.InterNetworkV6)
            _acceptorSocket.DualMode = OptionDualMode;

        // Bind the acceptor socket to the endpoint
        _acceptorSocket.Bind(Endpoint);

        // Refresh the endpoint property based on the actual endpoint created
        Endpoint = _acceptorSocket.LocalEndPoint!;

        // Call the server starting handler
        OnStarting();

        // Start listen to the acceptor socket with the given accepting backlog size
        _acceptorSocket.Listen(OptionAcceptorBacklog);

        // Reset statistic
        BytesPendingStat = 0;
        BytesSentStat = 0;
        BytesReceivedStat = 0;

        // Update the started flag
        IsStarted = true;

        // Call the server started handler
        OnStarted();

        // Perform the first server accept
        IsAccepting = true;
        StartAccept(_acceptorEventArg);

        return true;
    }


    /// <summary>
    /// Stop the server
    /// </summary>
    /// <returns>'true' if the server was successfully stopped, 'false' if the server is already stopped</returns>
    public virtual bool Stop()
    {
        Debug.Assert(IsStarted, "SSL server is not started!");
        if (!IsStarted)
            return false;

        // Stop accepting new clients
        IsAccepting = false;

        // Reset acceptor event arg
        _acceptorEventArg!.Completed -= OnSocketAcceptorCompletedAsync;

        // Call the server stopping handler
        OnStopping();

        try
        {
            // Close the acceptor socket
            _acceptorSocket!.Close();

            // Dispose the acceptor socket
            _acceptorSocket.Dispose();

            // Dispose event arguments
            _acceptorEventArg.Dispose();

            // Update the acceptor socket disposed flag
            IsSocketDisposed = true;
        }
        catch (ObjectDisposedException)
        {
        }

        // Disconnect all sessions
        DisconnectAll();

        // Update the started flag
        IsStarted = false;

        // Call the server stopped handler
        OnStopped();

        return true;
    }


    /// <summary>
    /// Restart the server
    /// </summary>
    /// <returns>'true' if the server was successfully restarted, 'false' if the server failed to restart</returns>
    public virtual bool Restart()
    {
        if (!Stop())
            return false;

        while (IsStarted)
            Thread.Yield();

        return Start();
    }

#endregion


#region Accepting clients

    /// <summary>
    /// Start accept a new client connection
    /// </summary>
    private void StartAccept(SocketAsyncEventArgs e)
    {
        // Socket must be cleared since the context object is being reused
        e.AcceptSocket = null;

        // Async accept a new client connection
        if (!_acceptorSocket!.AcceptAsync(e))
            ProcessAccept(e);
    }


    /// <summary>
    /// Process accepted client connection
    /// </summary>
    private void ProcessAccept(SocketAsyncEventArgs e)
    {
        // Check for errors
        if (e.SocketError == SocketError.Success)
        {
            Socket socket = e.AcceptSocket!;

            if (AcceptClient(socket))
            {
                // Create a new session to register
                SslSession session = CreateSession();

                // Register the session
                RegisterSession(session);

                // Connect new session
                session.Connect(socket);
            }
            else
            {
                // Close the socket
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                    socket.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
        else
            SendError(e.SocketError);

        // Accept the next client connection
        if (IsAccepting)
            StartAccept(e);
    }


    /// <summary>
    /// This method is the callback method associated with Socket.AcceptAsync()
    /// operations and is invoked when an accept operation is complete
    /// </summary>
    private void OnSocketAcceptorCompletedAsync(object? sender, SocketAsyncEventArgs e)
    {
        if (IsSocketDisposed)
            return;

        ProcessAccept(e);
    }

#endregion


#region Session factory

    /// <summary>
    /// Create SSL session factory method
    /// </summary>
    /// <returns>SSL session</returns>
    protected virtual SslSession CreateSession() => new(this);

#endregion


#region Session management


    /// <summary>
    /// Disconnect all connected sessions
    /// </summary>
    /// <returns>'true' if all sessions were successfully disconnected, 'false' if the server is not started</returns>
    public virtual bool DisconnectAll()
    {
        if (!IsStarted)
            return false;

        // Disconnect all sessions
        foreach (SslSession session in Sessions.Values)
            session.Disconnect();

        return true;
    }


    /// <summary>
    /// Find a session with a given id.
    /// </summary>
    /// <param name="id">Session Id</param>
    /// <returns>Session with a given Id or null if the session it not connected</returns>
    public SslSession? FindSession(Guid id) => Sessions.GetValueOrDefault(id);


    /// <summary>
    /// Register a new session
    /// </summary>
    /// <param name="session">Session to register</param>
    internal void RegisterSession(SslSession session)
    {
        // Register a new session
        Sessions.TryAdd(session.Id, session);
    }


    /// <summary>
    /// Unregister session by Id
    /// </summary>
    /// <param name="id">Session Id</param>
    internal void UnregisterSession(Guid id)
    {
        // Unregister session by Id
        Sessions.TryRemove(id, out SslSession? _);
    }

#endregion


#region Multicasting

    /// <summary>
    /// Multicast data to all connected sessions
    /// </summary>
    /// <param name="buffer">Buffer to multicast</param>
    /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
    public virtual bool Multicast(byte[] buffer) => Multicast(buffer.AsSpan());


    /// <summary>
    /// Multicast data to all connected clients
    /// </summary>
    /// <param name="buffer">Buffer to multicast</param>
    /// <param name="offset">Buffer offset</param>
    /// <param name="size">Buffer size</param>
    /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
    public virtual bool Multicast(byte[] buffer, int offset, int size) => Multicast(buffer.AsSpan(offset, size));


    /// <summary>
    /// Multicast data to all connected clients
    /// </summary>
    /// <param name="buffer">Buffer to send as a span of bytes</param>
    /// <returns>'true' if the data was successfully multicasted, 'false' if the data was not multicasted</returns>
    public virtual bool Multicast(ReadOnlySpan<byte> buffer)
    {
        if (!IsStarted)
            return false;

        if (buffer.IsEmpty)
            return true;

        // Multicast data to all sessions
        foreach (SslSession session in Sessions.Values)
            session.SendAsync(buffer);

        return true;
    }

#endregion


#region Server handlers

    /// <summary>
    /// Accept client connection
    /// </summary>
    protected virtual bool AcceptClient(Socket client)
    {
        return true;
    }

    /// <summary>
    /// Handle server starting notification
    /// </summary>
    protected virtual void OnStarting()
    {
    }


    /// <summary>
    /// Handle server started notification
    /// </summary>
    protected virtual void OnStarted()
    {
    }


    /// <summary>
    /// Handle server stopping notification
    /// </summary>
    protected virtual void OnStopping()
    {
    }


    /// <summary>
    /// Handle server stopped notification
    /// </summary>
    protected virtual void OnStopped()
    {
    }


    /// <summary>
    /// Handle session connecting notification
    /// </summary>
    /// <param name="session">Connecting session</param>
    protected virtual void OnConnecting(SslSession session)
    {
    }


    /// <summary>
    /// Handle session connected notification
    /// </summary>
    /// <param name="session">Connected session</param>
    protected virtual void OnConnected(SslSession session)
    {
    }


    /// <summary>
    /// Handle session handshaking notification
    /// </summary>
    /// <param name="session">Handshaking session</param>
    protected virtual void OnHandshaking(SslSession session)
    {
    }


    /// <summary>
    /// Handle session handshaked notification
    /// </summary>
    /// <param name="session">Handshaked session</param>
    protected virtual void OnHandshaked(SslSession session)
    {
    }


    /// <summary>
    /// Handle session disconnecting notification
    /// </summary>
    /// <param name="session">Disconnecting session</param>
    protected virtual void OnDisconnecting(SslSession session)
    {
    }


    /// <summary>
    /// Handle session disconnected notification
    /// </summary>
    /// <param name="session">Disconnected session</param>
    protected virtual void OnDisconnected(SslSession session)
    {
    }


    /// <summary>
    /// Handle error notification
    /// </summary>
    /// <param name="error">Socket error code</param>
    protected virtual void OnError(SocketError error)
    {
    }


    internal void OnConnectingInternal(SslSession session)
    {
        OnConnecting(session);
    }


    internal void OnConnectedInternal(SslSession session)
    {
        OnConnected(session);
    }


    internal void OnHandshakingInternal(SslSession session)
    {
        OnHandshaking(session);
    }


    internal void OnHandshakedInternal(SslSession session)
    {
        OnHandshaked(session);
    }


    internal void OnDisconnectingInternal(SslSession session)
    {
        OnDisconnecting(session);
    }


    internal void OnDisconnectedInternal(SslSession session)
    {
        OnDisconnected(session);
    }

#endregion


#region Error handling

    /// <summary>
    /// Send error notification
    /// </summary>
    /// <param name="error">Socket error code</param>
    private void SendError(SocketError error)
    {
        // Skip disconnect errors
        if (error == SocketError.ConnectionAborted ||
            error == SocketError.ConnectionRefused ||
            error == SocketError.ConnectionReset ||
            error == SocketError.OperationAborted ||
            error == SocketError.Shutdown)
            return;

        OnError(error);
    }

#endregion


#region IDisposable implementation

    /// <summary>
    /// Disposed flag
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Acceptor socket disposed flag
    /// </summary>
    public bool IsSocketDisposed { get; private set; } = true;


    // Implement IDisposable.
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }


    protected virtual void Dispose(bool disposingManagedResources)
    {
        // The idea here is that Dispose(Boolean) knows whether it is
        // being called to do explicit cleanup (the Boolean is true)
        // versus being called due to a garbage collection (the Boolean
        // is false). This distinction is useful because, when being
        // disposed explicitly, the Dispose(Boolean) method can safely
        // execute code using reference type fields that refer to other
        // objects knowing for sure that these other objects have not been
        // finalized or disposed of yet. When the Boolean is false,
        // the Dispose(Boolean) method should not execute code that
        // refer to reference type fields because those objects may
        // have already been finalized."

        if (!IsDisposed)
        {
            if (disposingManagedResources)
            {
                // Dispose managed resources here...
                Stop();
            }

            // Dispose unmanaged resources here...

            // Set large fields to null here...

            // Mark as disposed.
            IsDisposed = true;
        }
    }

#endregion
}