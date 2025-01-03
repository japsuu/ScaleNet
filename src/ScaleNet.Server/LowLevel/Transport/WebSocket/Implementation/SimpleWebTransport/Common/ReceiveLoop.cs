using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

internal static class ReceiveLoop
{
    public readonly struct Config
    {
        public readonly Connection Conn;
        public readonly int MaxMessageSize;
        public readonly bool ExpectMask;
        public readonly ConcurrentQueue<Message> Queue;
        public readonly BufferPool BufferPool;


        public Config(Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, BufferPool bufferPool)
        {
            Conn = conn ?? throw new ArgumentNullException(nameof(conn));
            MaxMessageSize = maxMessageSize;
            ExpectMask = expectMask;
            Queue = queue ?? throw new ArgumentNullException(nameof(queue));
            BufferPool = bufferPool ?? throw new ArgumentNullException(nameof(bufferPool));
        }


        public void Deconstruct(out Connection conn, out int maxMessageSize, out bool expectMask, out ConcurrentQueue<Message> queue, out BufferPool bufferPool)
        {
            conn = Conn;
            maxMessageSize = MaxMessageSize;
            expectMask = ExpectMask;
            queue = Queue;
            bufferPool = BufferPool;
        }
    }


    public static void Loop(Config config)
    {
        (Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> queue, BufferPool _) = config;

        byte[] readBuffer = new byte[Constants.HEADER_SIZE + (expectMask ? Constants.MASK_SIZE : 0) + maxMessageSize];
        try
        {
            try
            {
                TcpClient client = conn.Client!;

                while (client.Connected)
                    ReadOneMessage(config, readBuffer);

                SimpleWebLog.Info($"{conn} Not Connected");
            }
            catch (Exception)
            {
                Utils.SleepForInterrupt();
                throw;
            }
        }
        catch (ThreadInterruptedException e)
        {
            SimpleWebLog.InfoException(e);
        }
        catch (ThreadAbortException e)
        {
            SimpleWebLog.InfoException(e);
        }
        catch (ObjectDisposedException e)
        {
            SimpleWebLog.InfoException(e);
        }
        catch (ReadHelperException e)
        {
            SimpleWebLog.InfoException(e);
        }
        catch (SocketException e)
        {
            // this could happen if wss client closes stream
            SimpleWebLog.Warn($"ReceiveLoop SocketException\n{e.Message}");
            queue.Enqueue(new Message(conn.ConnId, e));
        }
        catch (IOException e)
        {
            // this could happen if client disconnects
            SimpleWebLog.Warn($"ReceiveLoop IOException\n{e.Message}");
            queue.Enqueue(new Message(conn.ConnId, e));
        }
        catch (InvalidDataException e)
        {
            SimpleWebLog.Error($"Invalid data from {conn}: {e.Message}");
            queue.Enqueue(new Message(conn.ConnId, e));
        }
        catch (Exception e)
        {
            SimpleWebLog.Exception(e);
            queue.Enqueue(new Message(conn.ConnId, e));
        }
        finally
        {
            conn.Dispose();
        }
    }


    private static void ReadOneMessage(Config config, byte[] buffer)
    {
        (Connection conn, int maxMessageSize, bool expectMask, ConcurrentQueue<Message> _, BufferPool _) = config;
        Stream stream = conn.Stream!;

        int offset = 0;

        // read 2
        offset = ReadHelper.Read(stream, buffer, offset, Constants.HEADER_MIN_SIZE);

        // log after first blocking call
        SimpleWebLog.Verbose($"Message From {conn}");

        if (MessageProcessor.NeedToReadShortLength(buffer))
            offset = ReadHelper.Read(stream, buffer, offset, Constants.SHORT_LENGTH);

        MessageProcessor.ValidateHeader(buffer, maxMessageSize, expectMask);

        if (expectMask)
            offset = ReadHelper.Read(stream, buffer, offset, Constants.MASK_SIZE);

        int opcode = MessageProcessor.GetOpcode(buffer);
        int payloadLength = MessageProcessor.GetPayloadLength(buffer);

        SimpleWebLog.Verbose($"Header ln:{payloadLength} op:{opcode} mask:{expectMask}");
        SimpleWebLog.DumpBuffer($"Raw Header", buffer, 0, offset);

        int msgOffset = offset;
        ReadHelper.Read(stream, buffer, offset, payloadLength);

        switch (opcode)
        {
            case 2:
                HandleArrayMessage(config, buffer, msgOffset, payloadLength);
                break;
            case 8:
                HandleCloseMessage(config, buffer, msgOffset, payloadLength);
                break;
        }
    }


    private static void HandleArrayMessage(Config config, byte[] buffer, int msgOffset, int payloadLength)
    {
        (Connection conn, int _, bool expectMask, ConcurrentQueue<Message> queue, BufferPool bufferPool) = config;

        ArrayBuffer arrayBuffer = bufferPool.Take(payloadLength);

        if (expectMask)
        {
            int maskOffset = msgOffset - Constants.MASK_SIZE;

            // write the result of toggle directly into arrayBuffer to avoid 2nd copy call
            MessageProcessor.ToggleMask(buffer, msgOffset, arrayBuffer, payloadLength, buffer, maskOffset);
        }
        else
            arrayBuffer.CopyFrom(buffer, msgOffset, payloadLength);

        // dump after mask off
        SimpleWebLog.DumpBuffer($"Message", arrayBuffer);

        queue.Enqueue(new Message(conn.ConnId, arrayBuffer));
    }


    private static void HandleCloseMessage(Config config, byte[] buffer, int msgOffset, int payloadLength)
    {
        (Connection conn, int _, bool expectMask, ConcurrentQueue<Message> _, BufferPool _) = config;

        if (expectMask)
        {
            int maskOffset = msgOffset - Constants.MASK_SIZE;
            MessageProcessor.ToggleMask(buffer, msgOffset, payloadLength, buffer, maskOffset);
        }

        // dump after mask off
        SimpleWebLog.DumpBuffer($"Message", buffer, msgOffset, payloadLength);

        SimpleWebLog.Info($"Close: {GetCloseCode(buffer, msgOffset)} message:{GetCloseMessage(buffer, msgOffset, payloadLength)}");

        conn.Dispose();
    }


    private static string GetCloseMessage(byte[] buffer, int msgOffset, int payloadLength) => Encoding.UTF8.GetString(buffer, msgOffset + 2, payloadLength - 2);

    private static int GetCloseCode(byte[] buffer, int msgOffset) => (buffer[msgOffset + 0] << 8) | buffer[msgOffset + 1];
}