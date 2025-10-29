using System;

namespace YCC.AnalisisCogisInventario.Logging;

internal static class ConsoleLogger
{
    public static void Info(string message)
    {
        Console.WriteLine($"{Timestamp()} {message}");
    }

    public static void Success(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{Timestamp()} {message}");
        Console.ForegroundColor = prev;
    }

    public static void Warn(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{Timestamp()} {message}");
        Console.ForegroundColor = prev;
    }

    public static void Error(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"{Timestamp()} {message}");
        Console.ForegroundColor = prev;
    }

    private static string Timestamp() => $"[{DateTime.Now:HH:mm:ss}]";
}

