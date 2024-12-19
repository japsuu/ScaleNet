using System.Text;

namespace ScaleNet.Server.LowLevel.Transport.WebSocket.SimpleWebTransport.Common;

/// <summary>
/// Constant values that should never change
/// <para>
/// Some values are from https://tools.ietf.org/html/rfc6455
/// </para>
/// </summary>
internal static class Constants
{
    /// <summary>
    /// Header is at most 4 bytes
    /// <para>
    /// If message is less than 125 then header is 2 bytes, else header is 4 bytes
    /// </para>
    /// </summary>
    public const int HEADER_SIZE = 4;

    /// <summary>
    /// Smallest size of header
    /// <para>
    /// If message is less than 125 then header is 2 bytes, else header is 4 bytes
    /// </para>
    /// </summary>
    public const int HEADER_MIN_SIZE = 2;

    /// <summary>
    /// bytes for short length
    /// </summary>
    public const int SHORT_LENGTH = 2;

    /// <summary>
    /// Message mask is always 4 bytes
    /// </summary>
    public const int MASK_SIZE = 4;

    /// <summary>
    /// Max size of a message for length to be 1 byte long
    /// <para>
    /// payload length between 0-125
    /// </para>
    /// </summary>
    public const int BYTE_PAYLOAD_LENGTH = 125;

    /// <summary>
    /// if payload length is 126 when next 2 bytes will be the length
    /// </summary>
    public const int USHORT_PAYLOAD_LENGTH = 126;

    /// <summary>
    /// if payload length is 127 when next 8 bytes will be the length
    /// </summary>
    public const int ULONG_PAYLOAD_LENGTH = 127;

    /// <summary>
    /// Guid used for WebSocket Protocol
    /// </summary>
    public const string HANDSHAKE_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    public static readonly int HandshakeGuidLength = HANDSHAKE_GUID.Length;

    public static readonly byte[] HandshakeGuidBytes = Encoding.ASCII.GetBytes(HANDSHAKE_GUID);

    /// <summary>
    /// Handshake messages will end with \r\n\r\n
    /// </summary>
    public static readonly byte[] EndOfHandshake = [(byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n'];
}