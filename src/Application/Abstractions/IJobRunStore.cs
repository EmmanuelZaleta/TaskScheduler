namespace YCC.SapAutomation.Application.Abstractions
{
  public interface IJobRunStore
  {
    Task<long> StartAsync(string jobName, CancellationToken cancellationToken = default);
    Task CompleteAsync(long jobRunId, string status, string? message = null, CancellationToken cancellationToken = default);
  }
}
