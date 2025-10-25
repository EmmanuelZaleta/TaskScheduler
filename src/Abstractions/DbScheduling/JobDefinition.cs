namespace YCC.SapAutomation.Abstractions.DbScheduling;

public sealed class JobDefinition
{
  public int JobId { get; init; }
  public string Name { get; init; } = string.Empty;
  public string OperationCode { get; init; } = string.Empty;

  public string? Command { get; init; }
  public string? Arguments { get; init; }
  public string? WorkingDirectory { get; init; }
  public bool ShowWindow { get; init; }
  public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

  public string ScheduleType { get; init; } = "MINUTES";
  public int? IntervalMinutes { get; init; }
  public TimeSpan? RunAtTime { get; init; }
  public byte? DaysOfWeekMask { get; init; }
}
