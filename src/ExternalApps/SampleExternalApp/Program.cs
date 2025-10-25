using System.Diagnostics;

internal static class Program
{
  public static int Main(string[] args)
  {
    var parsed = ParseArgs(args);

    if (!string.IsNullOrWhiteSpace(parsed.Title))
      Console.Title = parsed.Title;
    else
      Console.Title = $"SampleExternalApp PID={Environment.ProcessId} {DateTime.Now:HH:mm:ss}";

    var greeting = Environment.GetEnvironmentVariable("SAMPLE_EXTERNAL_GREETING") ?? "Hola";
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {greeting} desde SampleExternalApp.");
    Console.ResetColor();

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WorkingDirectory: {Environment.CurrentDirectory}");
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Args: {string.Join(' ', args)}");

    if (parsed.SleepSeconds > 0)
    {
      Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Durmiendo {parsed.SleepSeconds}s...");
      Thread.Sleep(TimeSpan.FromSeconds(parsed.SleepSeconds));
    }

    if (!string.IsNullOrWhiteSpace(parsed.WritePath))
    {
      try
      {
        var path = Path.GetFullPath(parsed.WritePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, $"{DateTime.Now:O} Emmanuel Zaleta Escribio esta linea {Environment.NewLine}");
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Escribi en: {path}");
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error escribiendo archivo: {ex.Message}");
      }
    }

    if (parsed.HoldSeconds > 0)
    {
      Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Manteniendo ventana {parsed.HoldSeconds}s...");
      Thread.Sleep(TimeSpan.FromSeconds(parsed.HoldSeconds));
    }

    if (parsed.ExitCode != 0)
    {
      Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Saliendo con codigo {parsed.ExitCode}");
      return parsed.ExitCode;
    }

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Listo (exit 0)");
    return 0;
  }

  private static (int SleepSeconds, string WritePath, int ExitCode, int HoldSeconds, string? Title) ParseArgs(string[] args)
  {
    int sleep = 0; string write = string.Empty; int exit = 0; int hold = 0; string? title = null;
    for (int i = 0; i < args.Length; i++)
    {
      var a = args[i];
      if (a.Equals("--sleep", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i+1], out var s))
      { sleep = s; i++; continue; }
      if (a.Equals("--write", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
      { write = args[i+1]; i++; continue; }
      if (a.Equals("--exit", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i+1], out var e))
      { exit = e; i++; continue; }
      if (a.Equals("--hold", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i+1], out var h))
      { hold = h; i++; continue; }
      if (a.Equals("--title", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
      { title = args[i+1]; i++; continue; }
    }
    return (sleep, write, exit, hold, title);
  }
}
