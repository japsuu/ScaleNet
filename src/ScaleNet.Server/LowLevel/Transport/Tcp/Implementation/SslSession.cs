using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using ScaleNet.Common;
using ScaleNet.Common.LowLevel;

namespace ScaleNet.Server.LowLevel.Transport.Tcp;

/// <summary>
/// SSL session is used to read and write data from the connected SSL client
/// </summary>
/// <remarks>Thread-safe</remarks>
public class SslSession : IDisposable
{
    private bool _isDisconnecting;
    private SslStream? _sslStream;
    private Guid? _sslStreamId;

    // Receive buffer
    private bool _isReceiving;
    private ByteBuffer? _receiveBuffer;

    // Send buffer
    private bool _isSending;
    private readonly object _sendLock = new();
    private ByteBuffer? _sendBufferMain;
    private ByteBuffer? _sendBufferFlush;
    private long _sendBufferFlushOffset;

    /// <summary>
    /// Is the session connected?
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Is the session handshaked?
    /// </summary>
    public bool IsHandshaked { get; private set; }
    
    /// <summary>
    /// Session Id
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Server
    /// </summary>
    public SslServer Server { get; }

    /// <summary>
    /// Socket
    /// </summary>
    public Socket? Socket { get; private set; }

    /// <summary>
    /// Number of bytes pending sent by the session
    /// </summary>
    public long BytesPending { get; private set; }

    /// <summary>
    /// Number of bytes sending by the session
    /// </summary>
    public long BytesSending { get; private set; }

    /// <summary>
    /// Number of bytes sent by the session
    /// </summary>
    public long BytesSent { get; private set; }

    /// <summary>
    /// Number of bytes received by the session
    /// </summary>
    public long BytesReceived { get; private set; }

    /// <summary>
    /// Option: receive buffer limit
    /// </summary>
    public int OptionReceiveBufferLimit { get; set; } = 0;

    /// <summary>
    /// Option: receive buffer size
    /// </summary>
    public int OptionReceiveBufferSize { get; set; }

    /// <summary>
    /// Option: send buffer limit
    /// </summary>
    public int OptionSendBufferLimit { get; set; } = 0;

    /// <summary>
    /// Option: send buffer size
    /// </summary>
    public int OptionSendBufferSize { get; set; }

    
    /// <summary>
    /// Initialize the session with a given server
    /// </summary>
    /// <param name="server">SSL server</param>
    public SslSession(SslServer server)
    {
        Id = Guid.NewGuid();
        Server = server;
        OptionReceiveBufferSize = server.OptionReceiveBufferSize;
        OptionSendBufferSize = server.OptionSendBufferSize;
    }

#region Connect/Disconnect session
    
    /// <summary>
    /// Connect the session
    /// </summary>
    /// <param name="socket">Session socket</param>
    internal void Connect(Socket socket)
    {
        Socket = socket;

        // Update the session socket disposed flag
        IsSocketDisposed = false;

        // Setup buffers
        _receiveBuffer = new ByteBuffer();
        _sendBufferMain = new ByteBuffer();
        _sendBufferFlush = new ByteBuffer();

        // Apply the option: keep alive
        if (Server.OptionKeepAlive)
            Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
#if !NETSTANDARD // Not supported
        if (Server.OptionTcpKeepAliveTime >= 0)
            Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, Server.OptionTcpKeepAliveTime);
        if (Server.OptionTcpKeepAliveInterval >= 0)
            Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, Server.OptionTcpKeepAliveInterval);
        if (Server.OptionTcpKeepAliveRetryCount >= 0)
            Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, Server.OptionTcpKeepAliveRetryCount);
#endif

        // Apply the option: no delay
        if (Server.OptionNoDelay)
            Socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

        // Prepare receive & send buffers
        _receiveBuffer.Reserve(OptionReceiveBufferSize);
        _sendBufferMain.Reserve(OptionSendBufferSize);
        _sendBufferFlush.Reserve(OptionSendBufferSize);

        // Reset statistic
        BytesPending = 0;
        BytesSending = 0;
        BytesSent = 0;
        BytesReceived = 0;

        // Call the session connecting handler
        OnConnecting();

        // Call the session connecting handler in the server
        Server.OnConnectingInternal(this);

        // Update the connected flag
        IsConnected = true;

        // Call the session connected handler
        OnConnected();

        // Call the session connected handler in the server
        Server.OnConnectedInternal(this);

        try
        {
            // Create SSL stream
            _sslStreamId = Guid.NewGuid();
            _sslStream = Server.Context.CertificateValidationCallback != null
                ? new SslStream(new NetworkStream(Socket, false), false, Server.Context.CertificateValidationCallback)
                : new SslStream(new NetworkStream(Socket, false), false);

            // Call the session handshaking handler
            OnHandshaking();

            // Call the session handshaking handler in the server
            Server.OnHandshakingInternal(this);

            // Begin the SSL handshake
            _sslStream.BeginAuthenticateAsServer(
                Server.Context.Certificate, Server.Context.ClientCertificateRequired, ServerSslContext.Protocols, false, ProcessHandshake, _sslStreamId);
        }
        catch (Exception)
        {
            SendError(SocketError.NotConnected);
            Disconnect();
        }
    }


    /// <summary>
    /// Disconnect the session
    /// </summary>
    /// <returns>'true' if the section was successfully disconnected, 'false' if the section is already disconnected</returns>
    public virtual bool Disconnect()
    {
        if (!IsConnected)
            return false;

        if (_isDisconnecting)
            return false;

        // Update the disconnecting flag
        _isDisconnecting = true;

        // Call the session disconnecting handler
        OnDisconnecting();

        // Call the session disconnecting handler in the server
        Server.OnDisconnectingInternal(this);
        
        Debug.Assert(_sslStream != null, nameof(_sslStream) + " != null");
        Debug.Assert(Socket != null, nameof(Socket) + " != null");

        try
        {
            try
            {
                // Shutdown the SSL stream
                _sslStream.ShutdownAsync().Wait();
            }
            catch (Exception)
            {
                // ignored
            }

            // Dispose the SSL stream & buffer
            _sslStream.Dispose();
            _sslStreamId = null;

            try
            {
                // Shutdown the socket associated with the client
                Socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }

            // Close the session socket
            Socket.Close();

            // Dispose the session socket
            Socket.Dispose();

            // Update the session socket disposed flag
            IsSocketDisposed = true;
        }
        catch (ObjectDisposedException)
        {
        }

        // Update the handshaked flag
        IsHandshaked = false;

        // Update the connected flag
        IsConnected = false;

        // Update sending/receiving flags
        _isReceiving = false;
        _isSending = false;

        // Clear send/receive buffers
        ClearBuffers();

        // Call the session disconnected handler
        OnDisconnected();

        // Call the session disconnected handler in the server
        Server.OnDisconnectedInternal(this);

        // Unregister session
        Server.UnregisterSession(Id);

        // Reset the disconnecting flag
        _isDisconnecting = false;

        return true;
    }

#endregion


#region Send/Receive data

    /// <summary>
    /// Send data to the client (synchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send</param>
    /// <returns>Size of sent data</returns>
    public virtual int Send(byte[] buffer) => Send(buffer.AsSpan());


    /// <summary>
    /// Send data to the client (synchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send</param>
    /// <param name="offset">Buffer offset</param>
    /// <param name="size">Buffer size</param>
    /// <returns>Size of sent data</returns>
    public virtual int Send(byte[] buffer, int offset, int size) => Send(buffer.AsSpan(offset, size));


    /// <summary>
    /// Send data to the client (synchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send as a span of bytes</param>
    /// <returns>Size of sent data</returns>
    public virtual int Send(ReadOnlySpan<byte> buffer)
    {
        if (!IsHandshaked)
            return 0;

        if (buffer.IsEmpty)
            return 0;
        
        Debug.Assert(_sslStream != null, nameof(_sslStream) + " != null");

        try
        {
            // Sent data to the server
            _sslStream.Write(buffer);

            int sent = buffer.Length;

            // Update statistic
            BytesSent += sent;
            Interlocked.Add(ref Server.BytesSentStat, sent);

            // Call the buffer sent handler
            OnSent(sent, BytesPending + BytesSending);

            return sent;
        }
        catch (Exception)
        {
            SendError(SocketError.OperationAborted);
            Disconnect();
            return 0;
        }
    }


    /// <summary>
    /// Send data to the client (asynchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send</param>
    /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
    public virtual bool SendAsync(byte[] buffer) => SendAsync(buffer.AsSpan());


    /// <summary>
    /// Send data to the client (asynchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send</param>
    /// <param name="offset">Buffer offset</param>
    /// <param name="size">Buffer size</param>
    /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
    public virtual bool SendAsync(byte[] buffer, int offset, int size) => SendAsync(buffer.AsSpan(offset, size));


    /// <summary>
    /// Send data to the client (asynchronous)
    /// </summary>
    /// <param name="buffer">Buffer to send as a span of bytes</param>
    /// <returns>'true' if the data was successfully sent, 'false' if the session is not connected</returns>
    public virtual bool SendAsync(ReadOnlySpan<byte> buffer)
    {
        if (!IsHandshaked)
            return false;

        if (buffer.IsEmpty)
            return true;

        Debug.Assert(_sendBufferMain != null, nameof(_sendBufferMain) + " != null");
        
        lock (_sendLock)
        {
            // Check the send buffer limit
            if (_sendBufferMain.Size + buffer.Length > OptionSendBufferLimit && OptionSendBufferLimit > 0)
            {
                SendError(SocketError.NoBufferSpaceAvailable);
                return false;
            }

            // Fill the main send buffer
            _sendBufferMain.Append(buffer);

            // Update statistic
            BytesPending = _sendBufferMain.Size;

            // Avoid multiple send handlers
            if (_isSending)
                return true;
            _isSending = true;

            // Try to send the main buffer
            TrySend();
        }

        return true;
    }


    /// <summary>
    /// Receive data from the client (synchronous)
    /// </summary>
    /// <param name="buffer">Buffer to receive</param>
    /// <returns>Size of received data</returns>
    public virtual int Receive(byte[] buffer) => Receive(buffer, 0, buffer.Length);


    /// <summary>
    /// Receive data from the client (synchronous)
    /// </summary>
    /// <param name="buffer">Buffer to receive</param>
    /// <param name="offset">Buffer offset</param>
    /// <param name="size">Buffer size</param>
    /// <returns>Size of received data</returns>
    public virtual int Receive(byte[] buffer, int offset, int size)
    {
        if (!IsHandshaked)
            return 0;

        if (size == 0)
            return 0;
        
        Debug.Assert(_sslStream != null, nameof(_sslStream) + " != null");

        try
        {
            // Receive data from the client
            int received = _sslStream.Read(buffer, offset, size);
            if (received <= 0)
                return received;

            // Update statistic
            BytesReceived += received;
            Interlocked.Add(ref Server.BytesReceivedStat, received);

            // Call the buffer received handler
            OnReceived(buffer, 0, received);

            return received;
        }
        catch (Exception)
        {
            SendError(SocketError.OperationAborted);
            Disconnect();
            return 0;
        }
    }


    /// <summary>
    /// Receive text from the client (synchronous)
    /// </summary>
    /// <param name="size">Text size to receive</param>
    /// <returns>Received text</returns>
    public virtual string Receive(int size)
    {
        byte[] buffer = new byte[size];
        int length = Receive(buffer);
        return Encoding.UTF8.GetString(buffer, 0, length);
    }


    /// <summary>
    /// Receive data from the client (asynchronous)
    /// </summary>
    public virtual void ReceiveAsync()
    {
        // Try to receive data from the client
        TryReceive();
    }


    /// <summary>
    /// Try to receive new data
    /// </summary>
    private void TryReceive()
    {
        if (_isReceiving)
            return;

        if (!IsHandshaked)
            return;

        try
        {
            // Async receive with the receiver handler
            IAsyncResult result;
            do
            {
                if (!IsHandshaked)
                    return;

                _isReceiving = true;
                result = _sslStream!.BeginRead(_receiveBuffer!.Data, 0, (int)_receiveBuffer.Capacity, ProcessReceive, _sslStreamId);
            }
            while (result.CompletedSynchronously);
        }
        catch (ObjectDisposedException)
        {
        }
    }


    /// <summary>
    /// Try to send pending data
    /// </summary>
    private void TrySend()
    {
        if (!IsHandshaked)
            return;

        bool empty = false;

        lock (_sendLock)
        {
            // Is previous socket send in progress?
            if (_sendBufferFlush!.IsEmpty)
            {
                // Swap flush and main buffers
                _sendBufferFlush = Interlocked.Exchange(ref _sendBufferMain, _sendBufferFlush);
                _sendBufferFlushOffset = 0;

                // Update statistic
                BytesPending = 0;
                BytesSending += _sendBufferFlush!.Size;

                // Check if the flush buffer is empty
                if (_sendBufferFlush.IsEmpty)
                {
                    // Need to call empty send buffer handler
                    empty = true;

                    // End the sending process
                    _isSending = false;
                }
            }
            else
                return;
        }

        // Call the empty send buffer handler
        if (empty)
        {
            OnEmpty();
            return;
        }

        try
        {
            // Async write with the write handler
            _sslStream!.BeginWrite(_sendBufferFlush.Data, (int)_sendBufferFlushOffset, (int)(_sendBufferFlush.Size - _sendBufferFlushOffset), ProcessSend, _sslStreamId);
        }
        catch (ObjectDisposedException)
        {
        }
    }


    /// <summary>
    /// Clear send/receive buffers
    /// </summary>
    private void ClearBuffers()
    {
        lock (_sendLock)
        {
            // Clear send buffers
            _sendBufferMain?.Clear();
            _sendBufferFlush?.Clear();
            _sendBufferFlushOffset = 0;

            // Update statistic
            BytesPending = 0;
            BytesSending = 0;
        }
    }

#endregion


#region IO processing

    /// <summary>
    /// This method is invoked when an asynchronous handshake operation completes
    /// </summary>
    private void ProcessHandshake(IAsyncResult result)
    {
        try
        {
            if (IsHandshaked)
                return;

            // Validate SSL stream Id
            Guid? sslStreamId = result.AsyncState as Guid?;
            if (_sslStreamId != sslStreamId)
                return;

            // End the SSL handshake
            _sslStream!.EndAuthenticateAsServer(result);

            // Update the handshaked flag
            IsHandshaked = true;

            // Try to receive something from the client
            TryReceive();

            // Check the socket disposed state: in some rare cases it might be disconnected while receiving!
            if (IsSocketDisposed)
                return;

            // Call the session handshaked handler
            OnHandshaked();

            // Call the session handshaked handler in the server
            Server.OnHandshakedInternal(this);

            // Call the empty send buffer handler
            if (_sendBufferMain!.IsEmpty)
                OnEmpty();
        }
        catch (Exception)
        {
            SendError(SocketError.NotConnected);
            Disconnect();
        }
    }


    /// <summary>
    /// This method is invoked when an asynchronous receive operation completes
    /// </summary>
    private void ProcessReceive(IAsyncResult result)
    {
        try
        {
            if (!IsHandshaked)
                return;

            // Validate SSL stream Id
            Guid? sslStreamId = result.AsyncState as Guid?;
            if (_sslStreamId != sslStreamId)
                return;

            // End the SSL read
            int size = _sslStream!.EndRead(result);

            // Received some data from the client
            if (size > 0)
            {
                // Update statistic
                BytesReceived += size;
                Interlocked.Add(ref Server.BytesReceivedStat, size);

                // Call the buffer received handler
                OnReceived(_receiveBuffer!.Data, 0, size);

                // If the receive buffer is full increase its size
                if (_receiveBuffer.Capacity == size)
                {
                    // Check the receive buffer limit
                    if (2 * size > OptionReceiveBufferLimit && OptionReceiveBufferLimit > 0)
                    {
                        SendError(SocketError.NoBufferSpaceAvailable);
                        Disconnect();
                        return;
                    }

                    _receiveBuffer.Reserve(2 * size);
                }
            }

            _isReceiving = false;

            // If zero is returned from a read operation, the remote end has closed the connection
            if (size > 0)
            {
                if (!result.CompletedSynchronously)
                    TryReceive();
            }
            else
                Disconnect();
        }
        catch (Exception)
        {
            SendError(SocketError.OperationAborted);
            Disconnect();
        }
    }


    /// <summary>
    /// This method is invoked when an asynchronous send operation completes
    /// </summary>
    private void ProcessSend(IAsyncResult result)
    {
        try
        {
            // Validate SSL stream Id
            Guid? sslStreamId = result.AsyncState as Guid?;
            if (_sslStreamId != sslStreamId)
                return;

            if (!IsHandshaked)
                return;

            // End the SSL write
            _sslStream!.EndWrite(result);

            long size = _sendBufferFlush!.Size;

            // Send some data to the client
            if (size > 0)
            {
                // Update statistic
                BytesSending -= size;
                BytesSent += size;
                Interlocked.Add(ref Server.BytesSentStat, size);

                // Increase the flush buffer offset
                _sendBufferFlushOffset += size;

                // Successfully send the whole flush buffer
                if (_sendBufferFlushOffset == _sendBufferFlush.Size)
                {
                    // Clear the flush buffer
                    _sendBufferFlush.Clear();
                    _sendBufferFlushOffset = 0;
                }

                // Call the buffer sent handler
                OnSent(size, BytesPending + BytesSending);
            }

            // Try to send again if the session is valid
            TrySend();
        }
        catch (Exception)
        {
            SendError(SocketError.OperationAborted);
            Disconnect();
        }
    }

#endregion


#region Session handlers

    /// <summary>
    /// Handle client connecting notification
    /// </summary>
    protected virtual void OnConnecting()
    {
    }


    /// <summary>
    /// Handle client connected notification
    /// </summary>
    protected virtual void OnConnected()
    {
    }


    /// <summary>
    /// Handle client handshaking notification
    /// </summary>
    protected virtual void OnHandshaking()
    {
    }


    /// <summary>
    /// Handle client handshaked notification
    /// </summary>
    protected virtual void OnHandshaked()
    {
    }


    /// <summary>
    /// Handle client disconnecting notification
    /// </summary>
    protected virtual void OnDisconnecting()
    {
    }


    /// <summary>
    /// Handle client disconnected notification
    /// </summary>
    protected virtual void OnDisconnected()
    {
    }


    /// <summary>
    /// Handle buffer received notification
    /// </summary>
    /// <param name="buffer">Received buffer</param>
    /// <param name="offset">Received buffer offset</param>
    /// <param name="size">Received buffer size</param>
    /// <remarks>
    /// Notification is called when another part of buffer was received from the client
    /// </remarks>
    protected virtual void OnReceived(byte[] buffer, int offset, int size)
    {
    }


    /// <summary>
    /// Handle buffer sent notification
    /// </summary>
    /// <param name="sent">Size of sent buffer</param>
    /// <param name="pending">Size of pending buffer</param>
    /// <remarks>
    /// Notification is called when another part of buffer was sent to the client.
    /// This handler could be used to send another buffer to the client for instance when the pending size is zero.
    /// </remarks>
    protected virtual void OnSent(long sent, long pending)
    {
    }


    /// <summary>
    /// Handle empty send buffer notification
    /// </summary>
    /// <remarks>
    /// Notification is called when the send buffer is empty and ready for a new data to send.
    /// This handler could be used to send another buffer to the client.
    /// </remarks>
    protected virtual void OnEmpty()
    {
    }


    /// <summary>
    /// Handle error notification
    /// </summary>
    /// <param name="error">Socket error code</param>
    protected virtual void OnError(SocketError error)
    {
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
    /// Session socket disposed flag
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
                Disconnect();
            }

            // Dispose unmanaged resources here...

            // Set large fields to null here...

            // Mark as disposed.
            IsDisposed = true;
        }
    }

#endregion
}