using YCC.SapAutomation.Abstractions.ResourceThrottling;

namespace YCC.SapAutomation.Infrastructure.ResourceThrottling;

/// <summary>
/// Internal implementation of a resource lease.
/// </summary>
internal sealed class ResourceLease : IResourceLease
{
  private readonly Action _releaseAction;
  private bool _disposed;

  public string ResourceType { get; }
  public DateTime AcquiredAtUtc { get; }

  public ResourceLease(string resourceType, Action releaseAction)
  {
    ResourceType = resourceType;
    AcquiredAtUtc = DateTime.UtcNow;
    _releaseAction = releaseAction;
  }

  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;
    _releaseAction();
  }
}
