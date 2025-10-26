using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using YCC.SapAutomation.Abstractions.Automation;

namespace YCC.SapAutomation.Application.Automation;

public sealed class JobExecutionNotificationService : IJobExecutionNotificationService
{
  private readonly ConcurrentQueue<JobExecutionNotification> _executionHistory = new();
  private readonly int _maxHistorySize = 500;
  private readonly object _eventLock = new();
  private readonly ILogger<JobExecutionNotificationService> _logger;

  public event EventHandler<JobExecutionNotification>? JobExecutionChanged;

  public JobExecutionNotificationService(ILogger<JobExecutionNotificationService> logger)
  {
    _logger = logger;
  }

  public Task NotifyJobStartingAsync(string jobName)
  {
    var notification = new JobExecutionNotification
    {
      JobName = jobName,
      State = JobExecutionState.Starting,
      Timestamp = DateTime.UtcNow
    };

    EnqueueNotification(notification);
    _logger.LogInformation("Job {JobName} iniciando", jobName);
    return Task.CompletedTask;
  }

  public Task NotifyJobCompletedAsync(string jobName, TimeSpan duration, int exitCode, string? message = null)
  {
    var notification = new JobExecutionNotification
    {
      JobName = jobName,
      State = exitCode == 0 ? JobExecutionState.Completed : JobExecutionState.Failed,
      Timestamp = DateTime.UtcNow,
      Duration = duration,
      ExitCode = exitCode,
      Message = message
    };

    EnqueueNotification(notification);
    _logger.LogInformation("Job {JobName} completado. Duración: {Duration}ms. Exit: {ExitCode}",
      jobName, duration.TotalMilliseconds, exitCode);
    return Task.CompletedTask;
  }

  public Task NotifyJobFailedAsync(string jobName, string message)
  {
    var notification = new JobExecutionNotification
    {
      JobName = jobName,
      State = JobExecutionState.Failed,
      Timestamp = DateTime.UtcNow,
      Message = message
    };

    EnqueueNotification(notification);
    _logger.LogError("Job {JobName} falló: {Message}", jobName, message);
    return Task.CompletedTask;
  }

  public Task<IReadOnlyCollection<JobExecutionNotification>> GetRecentExecutionsAsync(int count = 50)
  {
    var recent = _executionHistory
      .OrderByDescending(n => n.Timestamp)
      .Take(Math.Min(count, _executionHistory.Count))
      .ToList();

    return Task.FromResult<IReadOnlyCollection<JobExecutionNotification>>(recent);
  }

  private void EnqueueNotification(JobExecutionNotification notification)
  {
    _executionHistory.Enqueue(notification);

    while (_executionHistory.Count > _maxHistorySize && _executionHistory.TryDequeue(out _))
    {
      // Limitando
    }

    lock (_eventLock)
    {
      JobExecutionChanged?.Invoke(this, notification);
    }
  }
}
