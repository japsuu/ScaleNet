﻿namespace Shared.Utils;

public static class MemoryDebugUtils
{
    public static string AsStringHex(this ArraySegment<byte> segment)
    {
        return string.Join(" ", segment.ToArray().Select(b => b.ToString("X2").PadLeft(2, '0')));
    }
    
    
    public static string AsStringDecimal(this ArraySegment<byte> segment)
    {
        return string.Join(" ", segment.ToArray().Select(b => b.ToString()));
    }
    
    
    public static string AsStringBits(this ArraySegment<byte> segment)
    {
        return string.Join(" ", segment.ToArray().Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
    }
    
    
    public static string AsStringHex(this ReadOnlyMemory<byte> segment)
    {
        return string.Join(" ", segment.ToArray().Select(b => b.ToString("X2").PadLeft(2, '0')));
    }
    
    
    public static string AsStringDecimal(this ReadOnlyMemory<byte> segment)
    {
        return string.Join(" ", segment.ToArray().Select(b => b.ToString()));
    }
    
    
    public static string AsStringBits(this ReadOnlyMemory<byte> segment)
    {
        return string.Join(" ", segment.ToArray().Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
    }
}