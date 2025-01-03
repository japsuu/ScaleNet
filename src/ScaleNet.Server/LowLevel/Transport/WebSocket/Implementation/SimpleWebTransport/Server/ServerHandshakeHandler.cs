using System.Security.Cryptography;
using System.Text;
using ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Server;

/// <summary>
/// Handles Handshakes from new clients on the server
/// <para>The server handshake has buffers to reduce allocations when clients connect</para>
/// </summary>
internal class ServerHandshakeHandler
{
    private const int GET_SIZE = 3;
    private const int RESPONSE_LENGTH = 129;
    private const int KEY_LENGTH = 24;
    private const int MERGED_KEY_LENGTH = 60;

    private const string KEY_HEADER_STRING = "Sec-WebSocket-Key: ";

    private readonly int _maxHttpHeaderSize;
    private readonly SHA1 _sha1 = SHA1.Create();
    private readonly BufferPool _bufferPool;


    public ServerHandshakeHandler(BufferPool bufferPool, int handshakeMaxSize)
    {
        _bufferPool = bufferPool;
        _maxHttpHeaderSize = handshakeMaxSize;
    }


    ~ServerHandshakeHandler()
    {
        _sha1.Dispose();
    }


    public bool TryHandshake(Common.Connection conn)
    {
        Stream stream = conn.Stream!;

        using (ArrayBuffer getHeader = _bufferPool.Take(GET_SIZE))
        {
            if (!ReadHelper.TryRead(stream, getHeader.Array, 0, GET_SIZE))
                return false;
            getHeader.Count = GET_SIZE;


            if (!IsGet(getHeader.Array))
            {
                SimpleWebLog.Warn($"First bytes from client was not 'GET' for handshake, instead was {SimpleWebLog.BufferToString(getHeader.Array, 0, GET_SIZE)}");
                return false;
            }
        }


        string? msg = ReadToEndForHandshake(stream);

        if (string.IsNullOrEmpty(msg))
            return false;

        try
        {
            AcceptHandshake(stream, msg);
            return true;
        }
        catch (ArgumentException e)
        {
            SimpleWebLog.InfoException(e);
            return false;
        }
    }


    private string? ReadToEndForHandshake(Stream stream)
    {
        using ArrayBuffer readBuffer = _bufferPool.Take(_maxHttpHeaderSize);
        int? readCountOrFail = ReadHelper.SafeReadTillMatch(stream, readBuffer.Array, 0, _maxHttpHeaderSize, Constants.EndOfHandshake);
        if (!readCountOrFail.HasValue)
            return null;

        int readCount = readCountOrFail.Value;

        string msg = Encoding.ASCII.GetString(readBuffer.Array, 0, readCount);
        SimpleWebLog.Verbose(msg);

        return msg;
    }


    private static bool IsGet(byte[] getHeader) =>
        // just check bytes here instead of using Encoding.ASCII
        getHeader[0] == 71 && // G
        getHeader[1] == 69 && // E
        getHeader[2] == 84; // T


    private void AcceptHandshake(Stream stream, string msg)
    {
        using ArrayBuffer keyBuffer = _bufferPool.Take(KEY_LENGTH + Constants.HandshakeGuidLength), responseBuffer = _bufferPool.Take(RESPONSE_LENGTH);
        GetKey(msg, keyBuffer.Array);
        AppendGuid(keyBuffer.Array);
        byte[] keyHash = CreateHash(keyBuffer.Array);
        CreateResponse(keyHash, responseBuffer.Array);

        stream.Write(responseBuffer.Array, 0, RESPONSE_LENGTH);
    }


    private static void GetKey(string msg, byte[] keyBuffer)
    {
        int start = msg.IndexOf(KEY_HEADER_STRING, StringComparison.Ordinal) + KEY_HEADER_STRING.Length;

        SimpleWebLog.Verbose($"Handshake Key: {msg.Substring(start, KEY_LENGTH)}");
        Encoding.ASCII.GetBytes(msg, start, KEY_LENGTH, keyBuffer, 0);
    }


    private static void AppendGuid(byte[] keyBuffer)
    {
        Buffer.BlockCopy(Constants.HandshakeGuidBytes, 0, keyBuffer, KEY_LENGTH, Constants.HandshakeGuidLength);
    }


    private byte[] CreateHash(byte[] keyBuffer)
    {
        SimpleWebLog.Verbose($"Handshake Hashing {Encoding.ASCII.GetString(keyBuffer, 0, MERGED_KEY_LENGTH)}");

        return _sha1.ComputeHash(keyBuffer, 0, MERGED_KEY_LENGTH);
    }


    private static void CreateResponse(byte[] keyHash, byte[] responseBuffer)
    {
        string keyHashString = Convert.ToBase64String(keyHash);

        // compiler should merge these strings into 1 string before format
        string message = "HTTP/1.1 101 Switching Protocols\r\n" +
                         "Connection: Upgrade\r\n" +
                         "Upgrade: websocket\r\n" +
                         $"Sec-WebSocket-Accept: {keyHashString}\r\n\r\n";

        SimpleWebLog.Verbose($"Handshake Response length {message.Length}, IsExpected {message.Length == RESPONSE_LENGTH}");
        Encoding.ASCII.GetBytes(message, 0, RESPONSE_LENGTH, responseBuffer, 0);
    }
}