using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace YCC.SapAutomation.Application.Jobs.ExternalProcess;

internal static class ExternalProcessExecutor
{
  public static async Task<int> RunAsync(ExternalProcessCommand command, ILogger logger, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);
    ArgumentNullException.ThrowIfNull(logger);

    if (string.IsNullOrWhiteSpace(command.Command))
      throw new InvalidOperationException("El comando del proceso externo es obligatorio.");

    var workingDirectory = string.IsNullOrWhiteSpace(command.WorkingDirectory)
      ? Directory.GetCurrentDirectory()
      : command.WorkingDirectory;

    var environment = command.Environment is { Count: > 0 }
      ? new Dictionary<string, string>(command.Environment, StringComparer.OrdinalIgnoreCase)
      : null;

    var startInfo = BuildStartInfo(command, workingDirectory, environment);

    using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

    var outputBuilder = new List<string>();
    var errorBuilder = new List<string>();

    var capture = !startInfo.UseShellExecute && (startInfo.RedirectStandardOutput || startInfo.RedirectStandardError);
    if (capture)
    {
      process.OutputDataReceived += (_, args) =>
      {
        if (!string.IsNullOrEmpty(args.Data))
          outputBuilder.Add(args.Data);
      };
      process.ErrorDataReceived += (_, args) =>
      {
        if (!string.IsNullOrEmpty(args.Data))
          errorBuilder.Add(args.Data);
      };
    }

    logger.LogInformation("Ejecutando proceso externo: {Command} {Arguments} (wd={WD})", command.Command, command.Arguments, startInfo.WorkingDirectory);

    process.Start();

    if (capture)
    {
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();
    }

    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

    if (!command.ShowWindow && outputBuilder.Count > 0)
    {
      logger.LogInformation("Salida proceso externo:\n{Output}", string.Join(Environment.NewLine, outputBuilder));
    }

    if (!command.ShowWindow && errorBuilder.Count > 0)
    {
      logger.LogWarning("Error proceso externo:\n{Error}", string.Join(Environment.NewLine, errorBuilder));
    }

    return process.ExitCode;
  }

  private static ProcessStartInfo BuildStartInfo(
    ExternalProcessCommand command,
    string workingDirectory,
    IReadOnlyDictionary<string, string>? environment)
  {
    if (command.ShowWindow && environment is { Count: > 0 })
    {
      var title = Path.GetFileNameWithoutExtension(command.Command);
      var sb = new System.Text.StringBuilder();

      foreach (var kv in environment)
      {
        sb.Append("set \"")
          .Append(kv.Key)
          .Append("=")
          .Append(kv.Value?.Replace("\"", "\\\"") ?? string.Empty)
          .AppendLine("\"");
        sb.Append("&& ");
      }

      sb.Append("start \"")
        .Append(title)
        .Append("\" /D \"")
        .Append(workingDirectory)
        .Append("\" /wait \"")
        .Append(command.Command)
        .Append("\"");

      if (!string.IsNullOrWhiteSpace(command.Arguments))
        sb.Append(' ').Append(command.Arguments);

      return new ProcessStartInfo
      {
        FileName = "cmd.exe",
        Arguments = "/c " + sb,
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardError = false,
        RedirectStandardOutput = false,
        CreateNoWindow = false,
        WindowStyle = ProcessWindowStyle.Normal
      };
    }

    var startInfo = new ProcessStartInfo
    {
      FileName = command.Command,
      Arguments = command.Arguments,
      WorkingDirectory = workingDirectory,
      UseShellExecute = command.ShowWindow,
      RedirectStandardError = !command.ShowWindow,
      RedirectStandardOutput = !command.ShowWindow,
      CreateNoWindow = !command.ShowWindow
    };

    if (!startInfo.UseShellExecute && environment is { Count: > 0 })
    {
      foreach (var (key, value) in environment)
      {
        startInfo.Environment[key] = value ?? string.Empty;
      }
    }

    return startInfo;
  }
}
