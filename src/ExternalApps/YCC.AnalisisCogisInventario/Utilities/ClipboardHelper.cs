using System;
using System.Runtime.InteropServices;

namespace YCC.AnalisisCogisInventario.Utilities;

internal static class ClipboardHelper
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    public static bool TrySetText(string text)
    {
        try
        {
            if (text == null) text = string.Empty;
            if (!OpenClipboard(IntPtr.Zero)) return false;
            try
            {
                if (!EmptyClipboard()) return false;
                var bytes = (text.Length + 1) * 2; // UTF-16 LE incl. null terminator
                var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                if (hGlobal == IntPtr.Zero) return false;
                try
                {
                    var target = GlobalLock(hGlobal);
                    if (target == IntPtr.Zero) return false;
                    try
                    {
                        // Copy string to unmanaged memory without 'unsafe'
                        var bytesArr = System.Text.Encoding.Unicode.GetBytes(text + "\0");
                        Marshal.Copy(bytesArr, 0, target, bytesArr.Length);
                    }
                    finally { GlobalUnlock(hGlobal); }
                    if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                    {
                        GlobalFree(hGlobal);
                        return false;
                    }
                    // ownership transferred to clipboard on success
                    hGlobal = IntPtr.Zero;
                    return true;
                }
                finally
                {
                    if (hGlobal != IntPtr.Zero) GlobalFree(hGlobal);
                }
            }
            finally { CloseClipboard(); }
        }
        catch { return false; }
    }
}
