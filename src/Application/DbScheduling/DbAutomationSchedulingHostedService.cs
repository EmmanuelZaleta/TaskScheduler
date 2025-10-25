using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using YCC.SapAutomation.Abstractions.DbScheduling;
using YCC.SapAutomation.Application.Jobs.ExternalProcess;

namespace YCC.SapAutomation.Application.DbScheduling;

public sealed class DbAutomationSchedulingHostedService : BackgroundService
{
  private readonly IJobDefinitionStore _store;
  private readonly ISchedulerFactory _schedulerFactory;
  private readonly ILogger<DbAutomationSchedulingHostedService> _logger;
  private readonly IHostEnvironment _env;

  private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(60);
  private readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(30);

  private IScheduler? _scheduler;
  private Dictionary<int, string>? _lastSignatures;

  public DbAutomationSchedulingHostedService(
    IJobDefinitionStore store,
    ISchedulerFactory schedulerFactory,
    ILogger<DbAutomationSchedulingHostedService> logger,
    IHostEnvironment env)
  {
    _store = store;
    _schedulerFactory = schedulerFactory;
    _logger = logger;
    _env = env;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await EnsureSchedulerAsync(stoppingToken).ConfigureAwait(false);
        await Task.Delay(_refreshInterval, stoppingToken).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        break;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al programar jobs desde base de datos. Reintentando en {Seconds}s.", _retryInterval.TotalSeconds);
        _lastSignatures = null;
        if (_scheduler is { } scheduler)
        {
          try
          {
            if (!scheduler.IsShutdown)
              await scheduler.Standby().ConfigureAwait(false);
          }
          catch (Exception standbyEx)
          {
            _logger.LogDebug(standbyEx, "No se pudo poner en standby el scheduler tras un fallo.");
          }
        }

        await Task.Delay(_retryInterval, stoppingToken).ConfigureAwait(false);
      }
    }
  }

  public override async Task StopAsync(CancellationToken cancellationToken)
  {
    if (_scheduler is { } scheduler && !scheduler.IsShutdown)
      await scheduler.Standby().ConfigureAwait(false);

    await base.StopAsync(cancellationToken).ConfigureAwait(false);
  }

  private async Task EnsureSchedulerAsync(CancellationToken cancellationToken)
  {
    var defs = await _store.LoadEnabledAsync(cancellationToken).ConfigureAwait(false);

    if (defs.Count == 0)
    {
      _logger.LogWarning("No hay jobs habilitados en BD.");
      _lastSignatures = null;
      return;
    }

    _scheduler ??= await _schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);

    var signatures = BuildSignatureMap(defs);

    var shouldReload = _lastSignatures is null || !SignaturesEqual(_lastSignatures, signatures);

    if (shouldReload)
    {
      await RegisterDefinitionsAsync(_scheduler, defs, cancellationToken).ConfigureAwait(false);
      _lastSignatures = signatures;
    }

    if (_scheduler.InStandbyMode || !_scheduler.IsStarted)
      await _scheduler.Start(cancellationToken).ConfigureAwait(false);
  }

  private IJobDetail BuildJobDetail(JobDefinition d)
  {
    var workingDir = string.IsNullOrWhiteSpace(d.WorkingDirectory) ? _env.ContentRootPath : ResolvePath(d.WorkingDirectory!);
    var command = string.IsNullOrWhiteSpace(d.Command) ? ResolveDefaultCommand(d.OperationCode) : ResolvePath(d.Command!);

    var data = new JobDataMap
    {
      { ExternalProcessJob.CommandKey, command },
      { ExternalProcessJob.ArgumentsKey, d.Arguments ?? string.Empty },
      { ExternalProcessJob.WorkingDirectoryKey, workingDir },
      { ExternalProcessJob.ShowWindowKey, d.ShowWindow.ToString() }
    };

    if (d.Environment.Count > 0)
    {
      var json = System.Text.Json.JsonSerializer.Serialize(d.Environment);
      data[ExternalProcessJob.EnvironmentKey] = json;
    }

    return JobBuilder.Create<ExternalProcessJob>()
      .WithIdentity($"Job.{d.JobId}.{d.Name}")
      .UsingJobData(data)
      .Build();
  }

  private async Task RegisterDefinitionsAsync(IScheduler scheduler, IReadOnlyCollection<JobDefinition> defs, CancellationToken cancellationToken)
  {
    var jobIdentities = new HashSet<string>(defs.Select(d => $"Job.{d.JobId}.{d.Name}"), StringComparer.OrdinalIgnoreCase);

    var existing = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.AnyGroup()).ConfigureAwait(false);
    var obsolete = existing.Where(k => k.Name.StartsWith("Job.", StringComparison.OrdinalIgnoreCase) && !jobIdentities.Contains(k.Name)).ToList();

    if (obsolete.Count > 0)
    {
      await scheduler.DeleteJobs(obsolete).ConfigureAwait(false);
    }

    foreach (var d in defs)
    {
      try
      {
        var detail = BuildJobDetail(d);
        var trigger = BuildTrigger(d, detail);
        await scheduler.ScheduleJob(detail, new HashSet<ITrigger> { trigger }, true, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Registrado {Name} ({Schedule})", d.Name, d.ScheduleType);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "No se pudo registrar el job {Name}", d.Name);
      }
    }
  }

  private ITrigger BuildTrigger(JobDefinition d, IJobDetail detail)
  {
    switch (d.ScheduleType.ToUpperInvariant())
    {
      case "MINUTES":
        var interval = Math.Max(1, d.IntervalMinutes ?? 5);
        return TriggerBuilder.Create()
          .WithIdentity($"Trig.{d.JobId}")
          .ForJob(detail)
          .StartNow()
          .WithSimpleSchedule(s => s.WithIntervalInMinutes(interval).RepeatForever())
          .Build();
      case "DAILY":
        var time = d.RunAtTime ?? new TimeSpan(0,0,0);
        var cronDaily = $"0 {time.Minutes} {time.Hours} * * ?";
        return TriggerBuilder.Create()
          .WithIdentity($"Trig.{d.JobId}")
          .ForJob(detail)
          .WithCronSchedule(cronDaily)
          .Build();
      case "WEEKLY":
        var t = d.RunAtTime ?? new TimeSpan(0,0,0);
        var days = MaskToQuartzDays(d.DaysOfWeekMask ?? 0);
        var cronWeekly = $"0 {t.Minutes} {t.Hours} ? * {days}"; // ? for day-of-month
        return TriggerBuilder.Create()
          .WithIdentity($"Trig.{d.JobId}")
          .ForJob(detail)
          .WithCronSchedule(cronWeekly)
          .Build();
      case "ONCE":
        var rt = d.RunAtTime ?? new TimeSpan(0,0,10);
        DateTimeOffset startAt = DateTimeOffset.Now.Date.Add(rt);
        if (startAt < DateTimeOffset.Now) startAt = DateTimeOffset.Now.AddSeconds(5);
        return TriggerBuilder.Create()
          .WithIdentity($"Trig.{d.JobId}")
          .ForJob(detail)
          .StartAt(startAt)
          .Build();
      default:
        // fallback: cada 5 min
        return TriggerBuilder.Create()
          .WithIdentity($"Trig.{d.JobId}")
          .ForJob(detail)
          .StartNow()
          .WithSimpleSchedule(s => s.WithIntervalInMinutes(5).RepeatForever())
          .Build();
    }
  }

  private string MaskToQuartzDays(byte mask)
  {
    var map = new[] { "SUN","MON","TUE","WED","THU","FRI","SAT" };
    var list = new List<string>();
    for (int i=0;i<7;i++)
    {
      var bit = 1 << i;
      if ((mask & bit) != 0) list.Add(map[i]);
    }
    return list.Count == 0 ? "MON-FRI" : string.Join(",", list);
  }

  private string ResolvePath(string path)
  {
    if (System.IO.Path.IsPathRooted(path)) return path;
    return System.IO.Path.GetFullPath(System.IO.Path.Combine(_env.ContentRootPath, path));
  }

  private string ResolveDefaultCommand(string operationCode)
  {
    // Intenta mapear via appsettings: Automation:DbCommandMap:{code}:{command,arguments,workingDirectory}
    // Por defecto: intenta scripts/<code>.cmd relativo al ContentRoot.
    var guess = System.IO.Path.Combine(_env.ContentRootPath, "scripts", operationCode + ".cmd");
    return guess;
  }

  private static Dictionary<int, string> BuildSignatureMap(IReadOnlyCollection<JobDefinition> defs)
  {
    var map = new Dictionary<int, string>(defs.Count);

    foreach (var d in defs)
    {
      var envSignature = string.Empty;
      if (d.Environment.Count > 0)
      {
        var ordered = d.Environment.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();
        foreach (var kv in ordered)
        {
          builder.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
        }
        envSignature = builder.ToString();
      }

      var signature = string.Join('|', new[]
      {
        d.Name,
        d.OperationCode,
        d.Command ?? string.Empty,
        d.Arguments ?? string.Empty,
        d.WorkingDirectory ?? string.Empty,
        d.ShowWindow.ToString(),
        d.ScheduleType,
        d.IntervalMinutes?.ToString() ?? string.Empty,
        d.RunAtTime?.ToString() ?? string.Empty,
        d.DaysOfWeekMask?.ToString() ?? string.Empty,
        envSignature
      });

      map[d.JobId] = signature;
    }

    return map;
  }

  private static bool SignaturesEqual(IReadOnlyDictionary<int, string> previous, IReadOnlyDictionary<int, string> current)
  {
    if (previous.Count != current.Count)
      return false;

    foreach (var kv in previous)
    {
      if (!current.TryGetValue(kv.Key, out var value))
        return false;

      if (!string.Equals(kv.Value, value, StringComparison.Ordinal))
        return false;
    }

    return true;
  }
}
