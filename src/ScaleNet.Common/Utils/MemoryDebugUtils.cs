using System;
using System.Linq;

namespace ScaleNet.Common.Utils
{
    public static class MemoryDebugUtils
    {
        public static string AsStringHex(this ArraySegment<byte> data)
        {
            return string.Join(" ", data.ToArray().Select(b => b.ToString("X2").PadLeft(2, '0')));
        }
    
    
        public static string AsStringDecimal(this ArraySegment<byte> data)
        {
            return string.Join(" ", data.ToArray().Select(b => b.ToString()));
        }
    
    
        public static string AsStringBits(this ArraySegment<byte> data)
        {
            return string.Join(" ", data.ToArray().Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        }
    
    
        public static string AsStringHex(this ReadOnlyMemory<byte> data)
        {
            return string.Join(" ", data.ToArray().Select(b => b.ToString("X2").PadLeft(2, '0')));
        }
    
    
        public static string AsStringDecimal(this ReadOnlyMemory<byte> data)
        {
            return string.Join(" ", data.ToArray().Select(b => b.ToString()));
        }
    
    
        public static string AsStringBits(this ReadOnlyMemory<byte> data)
        {
            return string.Join(" ", data.ToArray().Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        }
    
    
        public static string AsStringHex(this byte[] data)
        {
            return string.Join(" ", data.Select(b => b.ToString("X2").PadLeft(2, '0')));
        }
    
    
        public static string AsStringDecimal(this byte[] data)
        {
            return string.Join(" ", data.Select(b => b.ToString()));
        }
    
    
        public static string AsStringBits(this byte[] data)
        {
            return string.Join(" ", data.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        }
    }
}