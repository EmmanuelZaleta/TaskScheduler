namespace YCC.SapAutomation.Abstractions.Options;

/// <summary>
/// Configuration for resource throttling limits.
/// </summary>
public sealed class ResourceThrottlingOptions
{
  public const string SectionName = "ResourceLimits";

  /// <summary>
  /// Dictionary of resource type to max concurrent limit.
  /// Example: { "SapConnection": 5, "DatabaseConnection": 10 }
  /// </summary>
  public Dictionary<string, int> Limits { get; set; } = new()
  {
    { "Default", 4 }
  };

  /// <summary>
  /// Gets the limit for a specific resource type, or the default if not found.
  /// </summary>
  public int GetLimit(string resourceType)
  {
    return Limits.TryGetValue(resourceType, out var limit) ? limit : Limits.GetValueOrDefault("Default", 4);
  }
}
