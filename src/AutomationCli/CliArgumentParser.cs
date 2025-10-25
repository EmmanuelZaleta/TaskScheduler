using System.Globalization;

namespace YCC.SapAutomation.AutomationCli;

internal static class CliArgumentParser
{
  public static (string[] hostArgs, AutomationCliOptions options) Parse(string[] args)
  {
    var hostArgs = new List<string>();
    var manifestPaths = new List<string>();
    var respectCron = false;
    string? manifestsPath = null;

    for (var i = 0; i < args.Length; i++)
    {
      var arg = args[i];
      if (IsOption(arg, "manifest"))
      {
        var value = ExtractValue(arg, args, ref i);
        if (string.IsNullOrWhiteSpace(value))
          throw new ArgumentException("El parametro --manifest requiere una ruta.");

        manifestPaths.Add(NormalizePath(value));
        continue;
      }

      if (IsOption(arg, "manifestsPath") || IsOption(arg, "manifests-path"))
      {
        var value = ExtractValue(arg, args, ref i);
        if (string.IsNullOrWhiteSpace(value))
          throw new ArgumentException("El parametro --manifestsPath requiere una ruta.");

        manifestsPath = NormalizePath(value);
        continue;
      }

      if (IsOption(arg, "respect-cron"))
      {
        respectCron = true;
        continue;
      }

      hostArgs.Add(arg);
    }

    var options = new AutomationCliOptions
    {
      ManifestPaths = manifestPaths,
      RespectCronExpressions = respectCron,
      ManifestsPath = manifestsPath
    };

    return (hostArgs.ToArray(), options);
  }

  private static bool IsOption(string arg, string name) =>
    arg.Equals($"--{name}", StringComparison.OrdinalIgnoreCase) ||
    arg.StartsWith($"--{name}=", StringComparison.OrdinalIgnoreCase);

  private static string ExtractValue(string currentArg, string[] args, ref int index)
  {
    var eqIndex = currentArg.IndexOf('=', StringComparison.Ordinal);
    if (eqIndex >= 0)
    {
      return currentArg[(eqIndex + 1)..].Trim('"');
    }

    if (index + 1 >= args.Length)
      return string.Empty;

    index++;
    return args[index].Trim('"');
  }

  private static string NormalizePath(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
      return path;

    return Path.GetFullPath(path, Environment.CurrentDirectory);
  }
}
