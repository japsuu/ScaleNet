using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

namespace ScaleNet.Client.LowLevel.Transport.WebSocket.SimpleWebTransport.Client.StandAlone
{
    /// <summary>
    /// Handles Handshake to the server when it first connects
    /// <para>The client handshake does not need buffers to reduce allocations since it only happens once</para>
    /// </summary>
    internal class ClientHandshake
    {
        public static bool TryHandshake(Connection conn, Uri uri)
        {
            try
            {
                Stream stream = conn.Stream!;

                byte[] keyBuffer = new byte[16];
                using (RNGCryptoServiceProvider rng = new())
                {
                    rng.GetBytes(keyBuffer);
                }

                string key = Convert.ToBase64String(keyBuffer);
                string keySum = key + Constants.HANDSHAKE_GUID;
                byte[] keySumBytes = Encoding.ASCII.GetBytes(keySum);
                SimpleWebLog.Verbose($"Handshake Hashing {Encoding.ASCII.GetString(keySumBytes)}");

                byte[] keySumHash = SHA1.Create().ComputeHash(keySumBytes);

                string expectedResponse = Convert.ToBase64String(keySumHash);
                string handshake =
                    $"GET {uri.PathAndQuery} HTTP/1.1\r\n" +
                    $"Host: {uri.Host}:{uri.Port}\r\n" +
                    $"Upgrade: websocket\r\n" +
                    $"Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Key: {key}\r\n" +
                    $"Sec-WebSocket-Version: 13\r\n" +
                    "\r\n";
                byte[] encoded = Encoding.ASCII.GetBytes(handshake);
                stream.Write(encoded, 0, encoded.Length);

                byte[] responseBuffer = new byte[1000];

                int? lengthOrNull = ReadHelper.SafeReadTillMatch(stream, responseBuffer, 0, responseBuffer.Length, Constants.EndOfHandshake);

                if (!lengthOrNull.HasValue)
                {
                    SimpleWebLog.Error("Connected closed before handshake");
                    return false;
                }

                string responseString = Encoding.ASCII.GetString(responseBuffer, 0, lengthOrNull.Value);

                const string acceptHeader = "Sec-WebSocket-Accept: ";
                int startIndex = responseString.IndexOf(acceptHeader, StringComparison.InvariantCultureIgnoreCase) + acceptHeader.Length;
                int endIndex = responseString.IndexOf("\r\n", startIndex, StringComparison.InvariantCultureIgnoreCase);
                string responseKey = responseString.Substring(startIndex, endIndex - startIndex);

                if (responseKey == expectedResponse)
                    return true;
                
                SimpleWebLog.Error($"Response key incorrect, Response:{responseKey} Expected:{expectedResponse}");
                return false;

            }
            catch (Exception e)
            {
                SimpleWebLog.Exception(e);
                return false;
            }
        }
    }
}