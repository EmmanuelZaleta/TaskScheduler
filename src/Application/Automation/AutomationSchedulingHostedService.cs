using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;

namespace YCC.SapAutomation.Application.Automation
{
  public sealed class AutomationSchedulingHostedService : IHostedService
  {
    private readonly IAutomationManifestProvider _manifestProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<AutomationSchedulingHostedService> _logger;
    private readonly AutomationJobFactory _jobFactory;

    public AutomationSchedulingHostedService(
      IAutomationManifestProvider manifestProvider,
      ISchedulerFactory schedulerFactory,
      ILogger<AutomationSchedulingHostedService> logger,
      AutomationJobFactory jobFactory)
    {
      _manifestProvider = manifestProvider;
      _schedulerFactory = schedulerFactory;
      _logger = logger;
      _jobFactory = jobFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      var manifests = await _manifestProvider.LoadAsync(cancellationToken);
      if (manifests.Count == 0)
      {
        _logger.LogWarning("No se encontraron manifiestos de automatizacion. Verifica la carpeta configurada.");
        return;
      }

      var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
      foreach (var manifest in manifests.Where(m => m.Enabled))
      {
        try
        {
          var jobDetail = _jobFactory.CreateJobDetail(manifest);
          var trigger = TriggerBuilder.Create()
            .WithIdentity($"{manifest.Name}.trigger")
            .WithDescription(manifest.Description)
            .WithCronSchedule(manifest.Cron)
            .Build();

          await scheduler.ScheduleJob(jobDetail, new HashSet<ITrigger> { trigger }, true, cancellationToken);
          _logger.LogInformation("Registrada automatizacion {Name} ({Kind})", manifest.Name, manifest.Kind);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "No se pudo registrar la automatizacion {Name}.", manifest.Name);
        }
      }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
  }
}
