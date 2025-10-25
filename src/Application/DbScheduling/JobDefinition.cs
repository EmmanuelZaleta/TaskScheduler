namespace YCC.SapAutomation.Application.DbScheduling;

public sealed class JobDefinition
{
  public int JobId { get; init; }
  public string Name { get; init; } = string.Empty;
  public string OperationCode { get; init; } = string.Empty;

  // Process info
  public string? Command { get; init; }
  public string? Arguments { get; init; }
  public string? WorkingDirectory { get; init; }
  public bool ShowWindow { get; init; }
  public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

  // Schedule info
  public string ScheduleType { get; init; } = "MINUTES"; // MINUTES, DAILY, WEEKLY, ONCE
  public int? IntervalMinutes { get; init; }
  public TimeSpan? RunAtTime { get; init; }
  public byte? DaysOfWeekMask { get; init; }
}

