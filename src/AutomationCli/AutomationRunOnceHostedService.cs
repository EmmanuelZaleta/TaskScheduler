using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Listener;
using YCC.SapAutomation.Application.Automation;

namespace YCC.SapAutomation.AutomationCli;

internal sealed class AutomationRunOnceHostedService : IHostedService
{
  private readonly IAutomationManifestProvider _manifestProvider;
  private readonly AutomationJobFactory _jobFactory;
  private readonly ISchedulerFactory _schedulerFactory;
  private readonly ILogger<AutomationRunOnceHostedService> _logger;
  private readonly IHostApplicationLifetime _lifetime;
  private readonly AutomationCliOptions _options;
  private readonly JsonSerializerOptions _jsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
  };

  public AutomationRunOnceHostedService(
    IAutomationManifestProvider manifestProvider,
    AutomationJobFactory jobFactory,
    ISchedulerFactory schedulerFactory,
    ILogger<AutomationRunOnceHostedService> logger,
    IHostApplicationLifetime lifetime,
    IOptions<AutomationCliOptions> options)
  {
    _manifestProvider = manifestProvider;
    _jobFactory = jobFactory;
    _schedulerFactory = schedulerFactory;
    _logger = logger;
    _lifetime = lifetime;
    _options = options.Value;
    _jsonOptions.Converters.Add(new JsonStringEnumConverter());
  }

  public async Task StartAsync(CancellationToken cancellationToken)
  {
    var manifests = await LoadManifestsAsync(cancellationToken);
    var enabledManifests = manifests.Where(m => m.Enabled).ToList();

    if (enabledManifests.Count == 0)
    {
      _logger.LogWarning("No se encontraron manifiestos habilitados para ejecutar.");
      _lifetime.StopApplication();
      return;
    }

    var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

    if (_options.RespectCronExpressions)
    {
      await ScheduleWithCronAsync(scheduler, enabledManifests, cancellationToken);
      _logger.LogInformation("Manifiestos registrados respetando sus cron expresions. El proceso permanecera en ejecucion.");
      return;
    }

    var scheduledJobs = await ScheduleRunOnceAsync(scheduler, enabledManifests, cancellationToken);
    if (scheduledJobs.Count == 0)
    {
      _logger.LogWarning("No se pudo programar ninguna automatizacion. Cerrando aplicacion.");
      _lifetime.StopApplication();
      return;
    }

    var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    scheduler.ListenerManager.AddJobListener(
      new CompletionListener(scheduledJobs, completion),
      GroupMatcher<JobKey>.AnyGroup());

    _ = Task.Run(async () =>
    {
      try
      {
        await completion.Task.ConfigureAwait(false);
        _logger.LogInformation("Todas las automatizaciones finalizaron. Cerrando aplicacion.");
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Ocurrio un error esperando la finalizacion de las automatizaciones.");
      }
      finally
      {
        _lifetime.StopApplication();
      }
    }, CancellationToken.None);
  }

  public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

  private async Task<IReadOnlyCollection<AutomationManifest>> LoadManifestsAsync(CancellationToken cancellationToken)
  {
    if (_options.ManifestPaths.Count > 0)
    {
      var list = new List<AutomationManifest>(_options.ManifestPaths.Count);
      foreach (var path in _options.ManifestPaths)
      {
        try
        {
          await using var stream = File.OpenRead(path);
          var manifest = await JsonSerializer.DeserializeAsync<AutomationManifest>(stream, _jsonOptions, cancellationToken);
          if (manifest is null)
          {
            _logger.LogWarning("El manifiesto {Path} no se pudo deserializar.", path);
            continue;
          }

          manifest = manifest with { SourcePath = path };
          if (string.IsNullOrWhiteSpace(manifest.Name))
          {
            manifest = manifest with { Name = Path.GetFileNameWithoutExtension(path) };
          }

          list.Add(manifest);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "No se pudo cargar el manifiesto {Path}.", path);
        }
      }

      return list;
    }

    return await _manifestProvider.LoadAsync(cancellationToken);
  }

  private async Task<HashSet<string>> ScheduleRunOnceAsync(IScheduler scheduler, IEnumerable<AutomationManifest> manifests, CancellationToken cancellationToken)
  {
    var scheduled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var manifest in manifests)
    {
      try
      {
        var jobDetail = _jobFactory.CreateJobDetail(manifest);
        var trigger = TriggerBuilder.Create()
          .WithIdentity($"{manifest.Name}.runonce")
          .WithDescription(manifest.Description)
          .ForJob(jobDetail)
          .StartNow()
          .Build();

        await scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
        scheduled.Add(jobDetail.Key.Name);
        _logger.LogInformation("Lanzada automatizacion {Name} (run-once).", manifest.Name);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "No se pudo programar la automatizacion {Name}.", manifest.Name);
      }
    }

    await scheduler.Start(cancellationToken);
    return scheduled;
  }

  private async Task ScheduleWithCronAsync(IScheduler scheduler, IEnumerable<AutomationManifest> manifests, CancellationToken cancellationToken)
  {
    foreach (var manifest in manifests)
    {
      try
      {
        var jobDetail = _jobFactory.CreateJobDetail(manifest);
        var trigger = TriggerBuilder.Create()
          .WithIdentity($"{manifest.Name}.cron")
          .WithDescription(manifest.Description)
          .WithCronSchedule(manifest.Cron)
          .ForJob(jobDetail)
          .Build();

        await scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);
        _logger.LogInformation("Registrada automatizacion {Name} con cron {Cron}.", manifest.Name, manifest.Cron);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "No se pudo registrar la automatizacion {Name}.", manifest.Name);
      }
    }

    await scheduler.Start(cancellationToken);
  }

  private sealed class CompletionListener : JobListenerSupport
  {
    private readonly HashSet<string> _targetJobNames;
    private readonly TaskCompletionSource<bool> _tcs;
    private readonly HashSet<string> _completed = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public CompletionListener(HashSet<string> targetJobNames, TaskCompletionSource<bool> tcs)
    {
      _targetJobNames = targetJobNames;
      _tcs = tcs;
    }

    public override string Name => "AutomationCliCompletionListener";

    public override Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
    {
      var jobName = context.JobDetail.Key.Name;
      if (!_targetJobNames.Contains(jobName))
      {
        return Task.CompletedTask;
      }

      lock (_lock)
      {
        // Tolerante a fallos: contar como terminado aunque falle.
        _completed.Add(jobName);

        if (_completed.IsSupersetOf(_targetJobNames))
        {
          _tcs.TrySetResult(true);
        }
      }

      return Task.CompletedTask;
    }
  }
}
