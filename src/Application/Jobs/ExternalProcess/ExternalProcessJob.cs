using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using YCC.SapAutomation.Abstractions.Automation;
using YCC.SapAutomation.Abstractions.ResourceThrottling;

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
    public const string ResourceTypeKey = "resourceType";

    private readonly ILogger<ExternalProcessJob> _logger;
    private readonly IJobExecutionNotificationService _notificationService;
    private readonly IResourceThrottlingManager _resourceThrottlingManager;

    public ExternalProcessJob(
      ILogger<ExternalProcessJob> logger,
      IJobExecutionNotificationService notificationService,
      IResourceThrottlingManager resourceThrottlingManager)
    {
      _logger = logger;
      _notificationService = notificationService;
      _resourceThrottlingManager = resourceThrottlingManager;
    }

    public async Task Execute(IJobExecutionContext context)
    {
      var jobName = context.JobDetail.Key.Name;
      var stopwatch = Stopwatch.StartNew();

      _logger.LogInformation("=== INICIANDO EJECUCIÓN DE JOB: {JobName} ===", jobName);

      // Notificar inicio
      await _notificationService.NotifyJobStartingAsync(jobName);

      try
      {
        var data = context.MergedJobDataMap;
        var command = data.GetString(CommandKey);
        var arguments = data.GetString(ArgumentsKey) ?? string.Empty;
        var workingDirectory = data.GetString(WorkingDirectoryKey);
        var environmentJson = data.GetString(EnvironmentKey);
        var resourceType = data.GetString(ResourceTypeKey);

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

        _logger.LogInformation("Ejecutando proceso: Command='{Command}', Args='{Args}', WorkDir='{WorkDir}', ShowWindow={Show}, ResourceType='{ResourceType}'",
          command, arguments, workingDirectory ?? Directory.GetCurrentDirectory(), showWindow, resourceType ?? "None");

        // Acquire resource if specified
        IResourceLease? resourceLease = null;
        if (!string.IsNullOrWhiteSpace(resourceType))
        {
          _logger.LogInformation("Adquiriendo recurso '{ResourceType}' para job '{JobName}'", resourceType, jobName);
          resourceLease = await _resourceThrottlingManager.AcquireAsync(resourceType, context.CancellationToken);
        }

        try
        {
          var exitCode = await ExternalProcessExecutor.RunAsync(request, _logger, context.CancellationToken);

          stopwatch.Stop();

          if (exitCode != 0)
          {
            _logger.LogError("El proceso externo finalizó con código de salida {ExitCode}.", exitCode);

            // Notificar fallo
            await _notificationService.NotifyJobCompletedAsync(
              jobName,
              stopwatch.Elapsed,
              exitCode,
              $"El proceso finalizó con código de salida {exitCode}");

            throw new InvalidOperationException($"El proceso externo finalizo con codigo {exitCode}.");
          }

          _logger.LogInformation("=== JOB COMPLETADO EXITOSAMENTE: {JobName} (ExitCode=0) ===", jobName);

          // Notificar éxito
          await _notificationService.NotifyJobCompletedAsync(
            jobName,
            stopwatch.Elapsed,
            0,
            "Ejecución completada exitosamente");
        }
        finally
        {
          // Release resource
          resourceLease?.Dispose();
        }
      }
      catch (Exception ex)
      {
        stopwatch.Stop();

        // Notificar fallo por excepción
        await _notificationService.NotifyJobFailedAsync(jobName, ex.Message);

        throw;
      }
    }
  }
}
