using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
      _logger.LogInformation("=== INICIANDO EJECUCIÓN DE JOB: {JobName} ===", context.JobDetail.Key.Name);

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

      Dictionary<string, string>? envDict = null;
      if (!string.IsNullOrWhiteSpace(environmentJson))
      {
        try { envDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(environmentJson); }
        catch (Exception ex) { _logger.LogWarning(ex, "No se pudieron cargar variables de entorno para el proceso externo."); }
      }

      var request = new ExternalProcessCommand(
        command,
        arguments,
        workingDirectory ?? Directory.GetCurrentDirectory(),
        showWindow,
        envDict);

      _logger.LogInformation("Ejecutando proceso: Command='{Command}', Args='{Args}', WorkDir='{WorkDir}', ShowWindow={Show}",
        command, arguments, workingDirectory ?? Directory.GetCurrentDirectory(), showWindow);

      var exitCode = await ExternalProcessExecutor.RunAsync(request, _logger, context.CancellationToken);

      if (exitCode != 0)
      {
        _logger.LogError("El proceso externo finalizó con código de salida {ExitCode}.", exitCode);
        throw new InvalidOperationException($"El proceso externo finalizo con codigo {exitCode}.");
      }

      _logger.LogInformation("=== JOB COMPLETADO EXITOSAMENTE: {JobName} (ExitCode=0) ===", context.JobDetail.Key.Name);
    }
  }
}
