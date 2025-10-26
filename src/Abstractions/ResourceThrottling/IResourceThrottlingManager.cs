namespace YCC.SapAutomation.Abstractions.ResourceThrottling;

/// <summary>
/// Manages throttling of jobs based on external resource constraints (e.g., SAP connections).
/// </summary>
public interface IResourceThrottlingManager
{
  /// <summary>
  /// Acquires a lease for the specified resource type. Waits if the limit has been reached.
  /// </summary>
  /// <param name="resourceType">The type of resource to acquire (e.g., "SapConnection").</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>A resource lease that must be disposed to release the resource.</returns>
  Task<IResourceLease> AcquireAsync(string resourceType, CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets the current usage count for a specific resource type.
  /// </summary>
  /// <param name="resourceType">The type of resource.</param>
  /// <returns>Current count of active leases for this resource type.</returns>
  int GetCurrentUsage(string resourceType);

  /// <summary>
  /// Gets the configured limit for a specific resource type.
  /// </summary>
  /// <param name="resourceType">The type of resource.</param>
  /// <returns>Maximum concurrent usage allowed for this resource type.</returns>
  int GetLimit(string resourceType);
}
