using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YCC.SapAutomation.Abstractions.DbScheduling;
using YCC.SapAutomation.Application.Jobs.ExternalProcess;
using YCC.SapAutomation.Sap.Options;

namespace YCC.SapAutomation.Application.Sap;

public sealed class SapBootstrapHostedService : BackgroundService
{
  private readonly IJobDefinitionStore _store;
  private readonly IHostEnvironment _env;
  private readonly ILogger<SapBootstrapHostedService> _logger;
  private readonly SapOptions _options;

  private bool _launchInProgress;

  public SapBootstrapHostedService(
    IJobDefinitionStore store,
    IHostEnvironment env,
    ILogger<SapBootstrapHostedService> logger,
    IOptions<SapOptions> options)
  {
    _store = store;
    _env = env;
    _logger = logger;
    _options = options.Value;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!OperatingSystem.IsWindows())
    {
      _logger.LogInformation("Bootstrap de SAP GUI deshabilitado: solo se admite en Windows.");
      return;
    }

    if (!string.Equals(_options.Mode, "Gui", StringComparison.OrdinalIgnoreCase))
    {
      _logger.LogDebug("Bootstrap de SAP GUI omitido porque Sap:Mode = {Mode}.", _options.Mode);
      return;
    }

    var gui = _options.Gui ?? new SapGuiOptions();
    if (!gui.BootstrapEnabled)
    {
      _logger.LogInformation("Bootstrap de SAP GUI deshabilitado via configuracion.");
      return;
    }

    if (string.IsNullOrWhiteSpace(gui.BootstrapOperationCode))
    {
      _logger.LogWarning("Sap:Gui:BootstrapOperationCode no esta configurado. Se omitira el bootstrap de SAP GUI.");
      return;
    }

    var monitorInterval = TimeSpan.FromSeconds(Math.Max(10, gui.MonitorIntervalSeconds));
    var processName = string.IsNullOrWhiteSpace(gui.ProcessName) ? "saplogon" : gui.ProcessName;

    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        if (!_launchInProgress && !IsSapRunning(processName))
        {
          await LaunchSapAsync(gui.BootstrapOperationCode, stoppingToken).ConfigureAwait(false);
        }
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error ejecutando el bootstrap de SAP GUI. Se reintentara en {Seconds}s.", monitorInterval.TotalSeconds);
      }

      await Task.Delay(monitorInterval, stoppingToken).ConfigureAwait(false);
    }
  }

  private bool IsSapRunning(string processName)
  {
    try
    {
      var processes = Process.GetProcessesByName(processName);
      var running = processes.Any(p => !p.HasExited);
      foreach (var p in processes) p.Dispose();

      if (running)
      {
        _logger.LogDebug("Se detecto el proceso {Process} en ejecucion. No se iniciara SAP GUI nuevamente.", processName);
      }

      return running;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "No se pudo verificar si el proceso {Process} esta activo.", processName);
      return false;
    }
  }

  private async Task LaunchSapAsync(string operationCode, CancellationToken cancellationToken)
  {
    _launchInProgress = true;
    try
    {
      var definition = await FindDefinitionAsync(operationCode, cancellationToken).ConfigureAwait(false);
      if (definition is null)
      {
        _logger.LogWarning("No se encontro un job habilitado con OperationCode {Code} en base de datos.", operationCode);
        return;
      }

      var command = BuildCommand(definition);
      _logger.LogInformation("Iniciando SAP GUI mediante job {Job} ({Operation}).", definition.Name, definition.OperationCode);
      var exitCode = await ExternalProcessExecutor.RunAsync(command, _logger, cancellationToken).ConfigureAwait(false);

      if (exitCode != 0)
      {
        _logger.LogWarning("El proceso configurado para iniciar SAP GUI finalizo con codigo {ExitCode}.", exitCode);
      }
      else
      {
        _logger.LogInformation("Bootstrap de SAP GUI completado correctamente.");
      }
    }
    finally
    {
      _launchInProgress = false;
    }
  }

  private async Task<JobDefinition?> FindDefinitionAsync(string operationCode, CancellationToken cancellationToken)
  {
    var defs = await _store.LoadEnabledAsync(cancellationToken).ConfigureAwait(false);
    return defs.FirstOrDefault(d => string.Equals(d.OperationCode, operationCode, StringComparison.OrdinalIgnoreCase));
  }

  private ExternalProcessCommand BuildCommand(JobDefinition definition)
  {
    var workingDir = string.IsNullOrWhiteSpace(definition.WorkingDirectory)
      ? _env.ContentRootPath
      : ResolvePath(definition.WorkingDirectory!);

    var command = string.IsNullOrWhiteSpace(definition.Command)
      ? ResolveDefaultCommand(definition.OperationCode)
      : ResolvePath(definition.Command!);

    return new ExternalProcessCommand(
      command,
      definition.Arguments ?? string.Empty,
      workingDir,
      definition.ShowWindow,
      definition.Environment);
  }

  private string ResolvePath(string path)
  {
    if (Path.IsPathRooted(path))
      return path;

    return Path.GetFullPath(Path.Combine(_env.ContentRootPath, path));
  }

  private string ResolveDefaultCommand(string operationCode)
  {
    return Path.Combine(_env.ContentRootPath, "scripts", operationCode + ".cmd");
  }
}
