namespace YCC.SapAutomation.Application.Options
{
  public sealed class SchedulerOptions
  {
    public const string SectionName = "Scheduler";

    public int MaxConcurrency { get; set; } = 4;
  }
}
