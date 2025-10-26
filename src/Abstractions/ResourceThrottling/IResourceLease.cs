namespace YCC.SapAutomation.Abstractions.ResourceThrottling;

/// <summary>
/// Represents a lease on a resource that must be disposed to release it.
/// </summary>
public interface IResourceLease : IDisposable
{
  /// <summary>
  /// The type of resource that was acquired.
  /// </summary>
  string ResourceType { get; }

  /// <summary>
  /// When the resource was acquired.
  /// </summary>
  DateTime AcquiredAtUtc { get; }
}
