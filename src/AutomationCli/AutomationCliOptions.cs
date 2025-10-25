namespace YCC.SapAutomation.AutomationCli;

internal sealed class AutomationCliOptions
{
  public IReadOnlyList<string> ManifestPaths { get; init; } = Array.Empty<string>();
  public bool RespectCronExpressions { get; init; }
  public string? ManifestsPath { get; init; }
}
