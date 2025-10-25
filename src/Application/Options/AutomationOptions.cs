namespace YCC.SapAutomation.Application.Options
{
  public sealed class AutomationOptions
  {
    public const string SectionName = "Automation";

    public string ManifestsPath { get; set; } = "automations";
  }
}
