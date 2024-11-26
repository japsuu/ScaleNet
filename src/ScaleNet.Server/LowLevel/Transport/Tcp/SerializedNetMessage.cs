using System.Buffers;

namespace ScaleNet.Server.LowLevel.Transport.Tcp;

/// <summary>
/// A raw packet of data.
/// </summary>
internal readonly struct SerializedNetMessage : IDisposable
{
    public readonly byte[] Data;


    public SerializedNetMessage(byte[] data)
    {
        Data = data;
    }


    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(Data);
    }
}