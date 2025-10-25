using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Quartz;

namespace YCC.SapAutomation.Application.Jobs.ExternalProcess
{
  [DisallowConcurrentExecution]
  public sealed class ExternalProcessJob : IJob
  {
    public const string CommandKey = "command";
    public const string ArgumentsKey = "arguments";
    public const string WorkingDirectoryKey = "workingDirectory";
    public const string EnvironmentKey = "environment";
    public const string ShowWindowKey = "showWindow";

    private readonly ILogger<ExternalProcessJob> _logger;

    public ExternalProcessJob(ILogger<ExternalProcessJob> logger)
    {
      _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
      var data = context.MergedJobDataMap;
      var command = data.GetString(CommandKey);
      var arguments = data.GetString(ArgumentsKey) ?? string.Empty;
      var workingDirectory = data.GetString(WorkingDirectoryKey);
      var environmentJson = data.GetString(EnvironmentKey);

      if (string.IsNullOrWhiteSpace(command))
        throw new InvalidOperationException("El comando del proceso externo es obligatorio.");

      var showWindow = false;
      var showWindowRaw = data.GetString(ShowWindowKey);
      if (!string.IsNullOrWhiteSpace(showWindowRaw))
        bool.TryParse(showWindowRaw, out showWindow);

      // Parse env vars (JSON)
      Dictionary<string, string>? envDict = null;
      if (!string.IsNullOrWhiteSpace(environmentJson))
      {
        try { envDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(environmentJson); }
        catch (Exception ex) { _logger.LogWarning(ex, "No se pudieron cargar variables de entorno para el proceso externo."); }
      }

      var hasEnv = envDict != null && envDict.Count > 0;
      var wd = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory;

      ProcessStartInfo startInfo;

      if (showWindow && hasEnv)
      {
        // Abrir nueva ventana respetando variables de entorno:
        // cmd.exe /c set VAR=... && ... && start "title" /D "wd" /wait "command" args
        var title = Path.GetFileNameWithoutExtension(command);
        var sb = new System.Text.StringBuilder();
        foreach (var kv in envDict!)
        {
          sb.Append("set \"").Append(kv.Key).Append("=").Append(kv.Value?.Replace("\"", "\\\"") ?? string.Empty).AppendLine("\"");
          sb.Append("&& ");
        }
        sb.Append("start \"").Append(title).Append("\" /D \"").Append(wd).Append("\" /wait \"")
          .Append(command).Append("\"");
        if (!string.IsNullOrWhiteSpace(arguments)) sb.Append(" ").Append(arguments);

        startInfo = new ProcessStartInfo
        {
          FileName = "cmd.exe",
          Arguments = "/c " + sb.ToString(),
          WorkingDirectory = wd,
          UseShellExecute = false,
          RedirectStandardError = false,
          RedirectStandardOutput = false,
          CreateNoWindow = false,
          WindowStyle = ProcessWindowStyle.Normal
        };
      }
      else
      {
        startInfo = new ProcessStartInfo
        {
          FileName = command,
          Arguments = arguments,
          WorkingDirectory = wd,
          UseShellExecute = showWindow,
          RedirectStandardError = !showWindow,
          RedirectStandardOutput = !showWindow,
          CreateNoWindow = !showWindow
        };

        if (!startInfo.UseShellExecute && hasEnv)
        {
          foreach (var (key, value) in envDict!) startInfo.Environment[key] = value;
        }
      }

      using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

      var outputBuilder = new List<string>();
      var errorBuilder = new List<string>();

      // Solo capturamos salida si realmente hay redireccion configurada
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

      _logger.LogInformation("Ejecutando proceso externo: {Command} {Arguments} (wd={WD})", command, arguments, startInfo.WorkingDirectory);
      process.Start();
      if (capture)
      {
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
      }

      await process.WaitForExitAsync(context.CancellationToken);

      if (!showWindow && outputBuilder.Count > 0)
      {
        _logger.LogInformation("Salida proceso externo:\n{Output}", string.Join(Environment.NewLine, outputBuilder));
      }

      if (!showWindow && errorBuilder.Count > 0)
      {
        _logger.LogWarning("Error proceso externo:\n{Error}", string.Join(Environment.NewLine, errorBuilder));
      }

      if (process.ExitCode != 0)
      {
        throw new InvalidOperationException($"El proceso externo finalizo con codigo {process.ExitCode}.");
      }
    }
  }
}
