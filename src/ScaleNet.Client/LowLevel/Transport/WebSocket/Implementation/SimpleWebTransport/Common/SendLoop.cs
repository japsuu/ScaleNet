using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common
{
    public static class SendLoopConfig
    {
        public static volatile bool BatchSend = false;
        public static volatile bool SleepBeforeSend = false;
    }

    internal static class SendLoop
    {
        public readonly struct Config
        {
            public readonly Connection Conn;
            public readonly int BufferSize;
            public readonly bool SetMask;


            public Config(Connection conn, int bufferSize, bool setMask)
            {
                Conn = conn ?? throw new ArgumentNullException(nameof(conn));
                BufferSize = bufferSize;
                SetMask = setMask;
            }


            public void Deconstruct(out Connection conn, out int bufferSize, out bool setMask)
            {
                conn = Conn;
                bufferSize = BufferSize;
                setMask = SetMask;
            }
        }


        public static void Loop(Config config)
        {
            (Connection conn, int bufferSize, bool setMask) = config;

            // create write buffer for this thread
            byte[] writeBuffer = new byte[bufferSize];
            MaskHelper? maskHelper = setMask ? new MaskHelper() : null;
            try
            {
                TcpClient? client = conn.Client;
                Stream stream = conn.Stream!;

                // null check incase disconnect while send thread is starting
                if (client == null)
                    return;

                while (client.Connected)
                {
                    // wait for message
                    conn.SendPending.Wait();

                    // wait for 1 ms to send other messages
                    if (SendLoopConfig.SleepBeforeSend)
                        Thread.Sleep(1);
                    conn.SendPending.Reset();

                    if (SendLoopConfig.BatchSend)
                    {
                        int offset = 0;
                        while (conn.SendQueue.TryDequeue(out ArrayBuffer? msg))
                        {
                            // check if connected before sending message
                            if (!client.Connected)
                            {
                                //SimpleWebLog.Info($"SendLoop {conn} not connected");
                                return;
                            }

                            int maxLength = msg.Count + Constants.HEADER_SIZE + Constants.MASK_SIZE;

                            // if next writer could overflow, write to stream and clear buffer
                            if (offset + maxLength > bufferSize)
                            {
                                stream.Write(writeBuffer, 0, offset);
                                offset = 0;
                            }

                            offset = SendMessage(writeBuffer, offset, msg, setMask, maskHelper);
                            msg.Release();
                        }

                        // after no message in queue, send remaining messages
                        // dont need to check offset > 0 because last message in queue will always be sent here

                        stream.Write(writeBuffer, 0, offset);
                    }
                    else
                    {
                        while (conn.SendQueue.TryDequeue(out ArrayBuffer? msg))
                        {
                            // check if connected before sending message
                            if (!client.Connected)
                            {
                                //SimpleWebLog.Info($"SendLoop {conn} not connected");
                                return;
                            }

                            int length = SendMessage(writeBuffer, 0, msg, setMask, maskHelper);
                            stream.Write(writeBuffer, 0, length);
                            msg.Release();
                        }
                    }
                }

                SimpleWebLog.Info($"{conn} Not Connected");
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
                conn.Dispose();
            }
        }


        /// <returns>new offset in buffer</returns>
        private static int SendMessage(byte[] buffer, int startOffset, ArrayBuffer msg, bool setMask, MaskHelper? maskHelper)
        {
            int msgLength = msg.Count;
            int offset = WriteHeader(buffer, startOffset, msgLength, setMask);

            if (setMask)
                offset = maskHelper!.WriteMask(buffer, offset);

            msg.CopyTo(buffer, offset);
            offset += msgLength;

            // dump before mask on
            //SimpleWebLog.DumpBuffer("Send", buffer, startOffset, offset);

            if (setMask)
            {
                int messageOffset = offset - msgLength;
                MessageProcessor.ToggleMask(buffer, messageOffset, msgLength, buffer, messageOffset - Constants.MASK_SIZE);
            }

            return offset;
        }


        private static int WriteHeader(byte[] buffer, int startOffset, int msgLength, bool setMask)
        {
            int sendLength = 0;
            const byte finished = 128;
            const byte byteOpCode = 2;

            buffer[startOffset + 0] = finished | byteOpCode;
            sendLength++;

            if (msgLength <= Constants.BYTE_PAYLOAD_LENGTH)
            {
                buffer[startOffset + 1] = (byte)msgLength;
                sendLength++;
            }
            else if (msgLength <= ushort.MaxValue)
            {
                buffer[startOffset + 1] = 126;
                buffer[startOffset + 2] = (byte)(msgLength >> 8);
                buffer[startOffset + 3] = (byte)msgLength;
                sendLength += 3;
            }
            else
                throw new InvalidDataException($"Trying to send a message larger than {ushort.MaxValue} bytes");

            if (setMask)
                buffer[startOffset + 1] |= 0b1000_0000;

            return sendLength + startOffset;
        }


        private sealed class MaskHelper
        {
            private readonly byte[] _maskBuffer = new byte[4];


            public int WriteMask(byte[] buffer, int offset)
            {
                RandomNumberGenerator.Fill(_maskBuffer);
                Buffer.BlockCopy(_maskBuffer, 0, buffer, offset, 4);

                return offset + 4;
            }
        }
    }
}