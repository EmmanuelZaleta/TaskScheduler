using System.ComponentModel.DataAnnotations;

namespace YCC.SapAutomation.Abstractions.Options;

public sealed class AutomationOptions
{
  public const string SectionName = "Automation";

  [Required]
  [MinLength(1)]
  public string ManifestsPath { get; set; } = "automations";
}
