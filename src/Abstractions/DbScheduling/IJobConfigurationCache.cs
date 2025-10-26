namespace YCC.SapAutomation.Abstractions.DbScheduling;

/// <summary>
/// Provides caching functionality for job configuration to enable fallback when database is unavailable.
/// </summary>
public interface IJobConfigurationCache
{
  /// <summary>
  /// Saves a snapshot of the current job configuration to disk.
  /// </summary>
  Task SaveSnapshotAsync(IEnumerable<JobDefinition> jobs, CancellationToken cancellationToken = default);

  /// <summary>
  /// Loads the most recent cached job configuration from disk.
  /// </summary>
  Task<IEnumerable<JobDefinition>> LoadSnapshotAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Checks if a valid cached configuration exists.
  /// </summary>
  bool HasValidSnapshot();

  /// <summary>
  /// Gets the timestamp of the last cached configuration.
  /// </summary>
  DateTime? GetSnapshotTimestamp();
}
