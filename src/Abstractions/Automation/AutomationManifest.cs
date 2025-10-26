using System.Text.Json.Serialization;

namespace YCC.SapAutomation.Abstractions.Automation;

public sealed record class AutomationManifest
{
  public string Name { get; init; } = string.Empty;
  public string Cron { get; init; } = string.Empty;
  public AutomationKind Kind { get; init; } = AutomationKind.DotNet;
  public string? Type { get; init; }
  public string? AssemblyPath { get; init; }
  public string? Command { get; init; }
  public string? Arguments { get; init; }
  public string? WorkingDirectory { get; init; }
  public Dictionary<string, string>? Environment { get; init; }
  public bool? ShowWindow { get; init; }
  public bool Enabled { get; init; } = true;
  public string? Description { get; init; }
  public string? ResourceType { get; init; }

  [JsonIgnore]
  public string? SourcePath { get; init; }
}
