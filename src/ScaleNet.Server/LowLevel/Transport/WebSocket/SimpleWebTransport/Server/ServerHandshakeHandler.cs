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
    const int GetSize = 3;
    const int ResponseLength = 129;
    const int KeyLength = 24;
    const int MergedKeyLength = 60;
    const string KeyHeaderString = "Sec-WebSocket-Key: ";
    // this isnt an offical max, just a reasonable size for a websocket handshake
    readonly int maxHttpHeaderSize = 3000;

    readonly SHA1 sha1 = SHA1.Create();
    readonly BufferPool bufferPool;

    public ServerHandshakeHandler(BufferPool bufferPool, int handshakeMaxSize)
    {
        this.bufferPool = bufferPool;
        this.maxHttpHeaderSize = handshakeMaxSize;
    }

    ~ServerHandshakeHandler()
    {
        sha1.Dispose();
    }

    public bool TryHandshake(Common.Connection conn)
    {
        Stream stream = conn.Stream;

        using (ArrayBuffer getHeader = bufferPool.Take(GetSize))
        {
            if (!ReadHelper.TryRead(stream, getHeader.Array, 0, GetSize))
                return false;
            getHeader.Count = GetSize;


            if (!IsGet(getHeader.Array))
            {
                //SimpleWebLog.Warn($"First bytes from client was not 'GET' for handshake, instead was {SimpleWebLog.BufferToString(getHeader.array, 0, GetSize)}");
                return false;
            }
        }


        string msg = ReadToEndForHandshake(stream);

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

    string ReadToEndForHandshake(Stream stream)
    {
        using (ArrayBuffer readBuffer = bufferPool.Take(maxHttpHeaderSize))
        {
            int? readCountOrFail = ReadHelper.SafeReadTillMatch(stream, readBuffer.Array, 0, maxHttpHeaderSize, Constants.EndOfHandshake);
            if (!readCountOrFail.HasValue)
                return null;

            int readCount = readCountOrFail.Value;

            string msg = Encoding.ASCII.GetString(readBuffer.Array, 0, readCount);
            SimpleWebLog.Verbose(msg);

            return msg;
        }
    }

    static bool IsGet(byte[] getHeader)
    {
        // just check bytes here instead of using Encoding.ASCII
        return getHeader[0] == 71 && // G
               getHeader[1] == 69 && // E
               getHeader[2] == 84;   // T
    }

    void AcceptHandshake(Stream stream, string msg)
    {
        using (
            ArrayBuffer keyBuffer = bufferPool.Take(KeyLength + Constants.HandshakeGuidLength),
            responseBuffer = bufferPool.Take(ResponseLength))
        {
            GetKey(msg, keyBuffer.Array);
            AppendGuid(keyBuffer.Array);
            byte[] keyHash = CreateHash(keyBuffer.Array);
            CreateResponse(keyHash, responseBuffer.Array);

            stream.Write(responseBuffer.Array, 0, ResponseLength);
        }
    }


    static void GetKey(string msg, byte[] keyBuffer)
    {
        int start = msg.IndexOf(KeyHeaderString) + KeyHeaderString.Length;

        SimpleWebLog.Verbose($"Handshake Key: {msg.Substring(start, KeyLength)}");
        Encoding.ASCII.GetBytes(msg, start, KeyLength, keyBuffer, 0);
    }

    static void AppendGuid(byte[] keyBuffer)
    {
        Buffer.BlockCopy(Constants.HandshakeGuidBytes, 0, keyBuffer, KeyLength, Constants.HandshakeGuidLength);
    }

    byte[] CreateHash(byte[] keyBuffer)
    {
        SimpleWebLog.Verbose($"Handshake Hashing {Encoding.ASCII.GetString(keyBuffer, 0, MergedKeyLength)}");

        return sha1.ComputeHash(keyBuffer, 0, MergedKeyLength);
    }

    static void CreateResponse(byte[] keyHash, byte[] responseBuffer)
    {
        string keyHashString = Convert.ToBase64String(keyHash);

        // compiler should merge these strings into 1 string before format
        string message = string.Format(
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Connection: Upgrade\r\n" +
            "Upgrade: websocket\r\n" +
            "Sec-WebSocket-Accept: {0}\r\n\r\n",
            keyHashString);

        SimpleWebLog.Verbose($"Handshake Response length {message.Length}, IsExpected {message.Length == ResponseLength}");
        Encoding.ASCII.GetBytes(message, 0, ResponseLength, responseBuffer, 0);
    }
}