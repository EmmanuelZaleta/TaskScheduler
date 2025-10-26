using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YCC.SapAutomation.Abstractions.Options;
using YCC.SapAutomation.Abstractions.ResourceThrottling;

namespace YCC.SapAutomation.Infrastructure.ResourceThrottling;

/// <summary>
/// Manages resource throttling using SemaphoreSlim pools per resource type.
/// </summary>
public sealed class ResourceThrottlingManager : IResourceThrottlingManager, IDisposable
{
  private readonly ILogger<ResourceThrottlingManager> _logger;
  private readonly ResourceThrottlingOptions _options;
  private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();
  private readonly ConcurrentDictionary<string, int> _currentUsage = new();

  public ResourceThrottlingManager(
    ILogger<ResourceThrottlingManager> logger,
    IOptions<ResourceThrottlingOptions> options)
  {
    _logger = logger;
    _options = options.Value;
  }

  public async Task<IResourceLease> AcquireAsync(string resourceType, CancellationToken cancellationToken = default)
  {
    // If no resource type specified, use default behavior (no throttling)
    if (string.IsNullOrWhiteSpace(resourceType))
    {
      _logger.LogDebug("No resource type specified, skipping throttling");
      return new ResourceLease("None", () => { });
    }

    var semaphore = GetOrCreateSemaphore(resourceType);
    var limit = _options.GetLimit(resourceType);

    _logger.LogInformation(
      "Attempting to acquire resource '{ResourceType}' (Current: {Current}/{Limit})",
      resourceType,
      GetCurrentUsage(resourceType),
      limit);

    var waitStarted = DateTime.UtcNow;
    await semaphore.WaitAsync(cancellationToken);
    var waitDuration = DateTime.UtcNow - waitStarted;

    _currentUsage.AddOrUpdate(resourceType, 1, (_, current) => current + 1);

    if (waitDuration.TotalSeconds > 1)
    {
      _logger.LogWarning(
        "Resource '{ResourceType}' acquired after waiting {WaitSeconds:F2}s (Current: {Current}/{Limit})",
        resourceType,
        waitDuration.TotalSeconds,
        GetCurrentUsage(resourceType),
        limit);
    }
    else
    {
      _logger.LogInformation(
        "Resource '{ResourceType}' acquired (Current: {Current}/{Limit})",
        resourceType,
        GetCurrentUsage(resourceType),
        limit);
    }

    return new ResourceLease(resourceType, () => Release(resourceType, semaphore));
  }

  public int GetCurrentUsage(string resourceType)
  {
    return _currentUsage.GetValueOrDefault(resourceType, 0);
  }

  public int GetLimit(string resourceType)
  {
    return _options.GetLimit(resourceType);
  }

  private void Release(string resourceType, SemaphoreSlim semaphore)
  {
    _currentUsage.AddOrUpdate(resourceType, 0, (_, current) => Math.Max(0, current - 1));
    semaphore.Release();

    _logger.LogInformation(
      "Resource '{ResourceType}' released (Current: {Current}/{Limit})",
      resourceType,
      GetCurrentUsage(resourceType),
      GetLimit(resourceType));
  }

  private SemaphoreSlim GetOrCreateSemaphore(string resourceType)
  {
    return _semaphores.GetOrAdd(resourceType, rt =>
    {
      var limit = _options.GetLimit(rt);
      _logger.LogInformation(
        "Creating resource pool for '{ResourceType}' with limit {Limit}",
        rt,
        limit);
      return new SemaphoreSlim(limit, limit);
    });
  }

  public void Dispose()
  {
    foreach (var semaphore in _semaphores.Values)
    {
      semaphore.Dispose();
    }
    _semaphores.Clear();
  }
}
