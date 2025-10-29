using System;
using System.Runtime.InteropServices;

namespace YCC.AnalisisCogisInventario.Utilities;

internal static class ComRot
{
    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid rclsid, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    public static object? TryGetActiveObject(string progId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(progId)) return null;
            if (CLSIDFromProgID(progId, out var clsid) != 0) return null;
            var hr = GetActiveObject(ref clsid, out var unk);
            return hr == 0 ? unk : null;
        }
        catch { return null; }
    }
}

