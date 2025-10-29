using System;
using System.Threading;

namespace YCC.AnalisisCogisInventario.Utilities;

internal static class Waiter
{
    public static bool WaitFor(Func<bool> condition, int timeoutSeconds, int intervalMs = 250)
    {
        var until = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < until)
        {
            try { if (condition()) return true; } catch { }
            Thread.Sleep(intervalMs);
        }
        return false;
    }
}

