namespace YCC.SapAutomation.Abstractions.Automation;

/// <summary>
/// Notificación de cambio en ejecución de job
/// </summary>
public sealed record JobExecutionNotification
{
  public string JobName { get; init; } = string.Empty;
  public JobExecutionState State { get; init; }
  public DateTime Timestamp { get; init; }
  public string? Message { get; init; }
  public TimeSpan? Duration { get; init; }
  public int? ExitCode { get; init; }
}

public enum JobExecutionState
{
  Starting = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4
}

/// <summary>
/// Servicio para notificar cambios de estado de jobs en tiempo real
/// </summary>
public interface IJobExecutionNotificationService
{
  event EventHandler<JobExecutionNotification>? JobExecutionChanged;

  Task NotifyJobStartingAsync(string jobName);
  Task NotifyJobCompletedAsync(string jobName, TimeSpan duration, int exitCode, string? message = null);
  Task NotifyJobFailedAsync(string jobName, string message);

  Task<IReadOnlyCollection<JobExecutionNotification>> GetRecentExecutionsAsync(int count = 50);
}
