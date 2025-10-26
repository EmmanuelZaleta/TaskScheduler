namespace YCC.SapAutomation.Abstractions.Options;

/// <summary>
/// Configuration options for job configuration caching.
/// </summary>
public sealed class JobConfigurationCacheOptions
{
  public const string SectionName = "JobConfigurationCache";

  /// <summary>
  /// Whether caching is enabled. Default is true.
  /// </summary>
  public bool Enabled { get; set; } = true;

  /// <summary>
  /// Path where the cache file will be stored. Default is "data/job-configuration-cache.json".
  /// </summary>
  public string Path { get; set; } = "data/job-configuration-cache.json";

  /// <summary>
  /// Maximum age of cache in hours before a warning is logged. Default is 24 hours.
  /// </summary>
  public int MaxAgeHours { get; set; } = 24;
}
