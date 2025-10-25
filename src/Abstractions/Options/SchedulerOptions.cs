using System.ComponentModel.DataAnnotations;

namespace YCC.SapAutomation.Abstractions.Options;

public sealed class SchedulerOptions
{
  public const string SectionName = "Scheduler";

  [Range(1, 100)]
  public int MaxConcurrency { get; set; } = 5;
}
