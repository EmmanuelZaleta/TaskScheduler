using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YCC.SapAutomation.Abstractions.DbScheduling;
using YCC.SapAutomation.Abstractions.Options;

namespace YCC.SapAutomation.Infrastructure.Persistence;

/// <summary>
/// File system implementation of job configuration cache.
/// </summary>
public sealed class FileSystemJobConfigurationCache : IJobConfigurationCache
{
  private readonly ILogger<FileSystemJobConfigurationCache> _logger;
  private readonly JobConfigurationCacheOptions _options;

  public FileSystemJobConfigurationCache(
    ILogger<FileSystemJobConfigurationCache> logger,
    IOptions<JobConfigurationCacheOptions> options)
  {
    _logger = logger;
    _options = options.Value;
  }

  public async Task SaveSnapshotAsync(IEnumerable<JobDefinition> jobs, CancellationToken cancellationToken = default)
  {
    if (!_options.Enabled)
    {
      _logger.LogDebug("Job configuration caching is disabled");
      return;
    }

    try
    {
      var snapshot = new ConfigurationSnapshot
      {
        Timestamp = DateTime.UtcNow,
        Jobs = jobs.ToList()
      };

      var directory = Path.GetDirectoryName(_options.Path);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
        _logger.LogInformation("Created cache directory: {Directory}", directory);
      }

      var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
      {
        WriteIndented = true
      });

      await File.WriteAllTextAsync(_options.Path, json, cancellationToken);

      _logger.LogInformation(
        "Job configuration snapshot saved to {Path} ({Count} jobs, Timestamp: {Timestamp})",
        _options.Path,
        snapshot.Jobs.Count,
        snapshot.Timestamp);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to save job configuration snapshot to {Path}", _options.Path);
    }
  }

  public async Task<IEnumerable<JobDefinition>> LoadSnapshotAsync(CancellationToken cancellationToken = default)
  {
    if (!_options.Enabled)
    {
      _logger.LogDebug("Job configuration caching is disabled");
      return Enumerable.Empty<JobDefinition>();
    }

    if (!File.Exists(_options.Path))
    {
      _logger.LogWarning("No cached configuration found at {Path}", _options.Path);
      return Enumerable.Empty<JobDefinition>();
    }

    try
    {
      var json = await File.ReadAllTextAsync(_options.Path, cancellationToken);
      var snapshot = JsonSerializer.Deserialize<ConfigurationSnapshot>(json);

      if (snapshot == null)
      {
        _logger.LogWarning("Failed to deserialize cached configuration from {Path}", _options.Path);
        return Enumerable.Empty<JobDefinition>();
      }

      var age = DateTime.UtcNow - snapshot.Timestamp;

      if (age.TotalHours > _options.MaxAgeHours)
      {
        _logger.LogWarning(
          "⚠️ Cached configuration is {Hours:F1} hours old (threshold: {MaxHours}h). Last updated: {Timestamp}",
          age.TotalHours,
          _options.MaxAgeHours,
          snapshot.Timestamp);
      }

      _logger.LogInformation(
        "Loaded cached job configuration from {Path} ({Count} jobs, Age: {Hours:F1}h)",
        _options.Path,
        snapshot.Jobs.Count,
        age.TotalHours);

      return snapshot.Jobs;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to load cached configuration from {Path}", _options.Path);
      return Enumerable.Empty<JobDefinition>();
    }
  }

  public bool HasValidSnapshot()
  {
    if (!_options.Enabled) return false;
    return File.Exists(_options.Path);
  }

  public DateTime? GetSnapshotTimestamp()
  {
    if (!_options.Enabled || !File.Exists(_options.Path))
      return null;

    try
    {
      var json = File.ReadAllText(_options.Path);
      var snapshot = JsonSerializer.Deserialize<ConfigurationSnapshot>(json);
      return snapshot?.Timestamp;
    }
    catch
    {
      return null;
    }
  }

  private sealed class ConfigurationSnapshot
  {
    public DateTime Timestamp { get; set; }
    public List<JobDefinition> Jobs { get; set; } = new();
  }
}
