using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using YCC.SapAutomation.Application.Abstractions;
using YCC.SapAutomation.Application.Jobs.ExternalProcess;
using YCC.SapAutomation.Application.Options;

namespace YCC.SapAutomation.Application.DbScheduling;

public sealed class DbAutomationSchedulingHostedService : IHostedService
{
  private readonly IJobDefinitionStore _store;
  private readonly ISchedulerFactory _schedulerFactory;
  private readonly ILogger<DbAutomationSchedulingHostedService> _logger;
  private readonly IHostEnvironment _env;
  private readonly IOptionsMonitor<AutomationOptions> _autoOptions;

  public DbAutomationSchedulingHostedService(
    IJobDefinitionStore store,
    ISchedulerFactory schedulerFactory,
    ILogger<DbAutomationSchedulingHostedService> logger,
    IHostEnvironment env,
    IOptionsMonitor<AutomationOptions> autoOptions)
  {
    _store = store;
    _schedulerFactory = schedulerFactory;
    _logger = logger;
    _env = env;
    _autoOptions = autoOptions;
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    var defs = await _store.LoadEnabledAsync(cancellationToken);
    if (defs.Count == 0)
    {
      _logger.LogWarning("No hay jobs habilitados en BD.");
      return;
    }

    var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

    foreach (var d in defs)
    {
      try
      {
        var detail = BuildJobDetail(d);
        var trigger = BuildTrigger(d, detail);
        await scheduler.ScheduleJob(detail, trigger, cancellationToken);
        _logger.LogInformation("Registrado {Name} ({Schedule})", d.Name, d.ScheduleType);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "No se pudo registrar el job {Name}", d.Name);
      }
    }

    await scheduler.Start(cancellationToken);
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
    var section = _autoOptions.CurrentValue?.ManifestsPath; // unused here; keep method simple
    // Por defecto: intenta scripts/<code>.cmd relativo al ContentRoot.
    var guess = System.IO.Path.Combine(_env.ContentRootPath, "scripts", operationCode + ".cmd");
    return guess;
  }
}
