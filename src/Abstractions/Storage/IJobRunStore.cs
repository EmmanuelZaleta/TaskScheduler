namespace YCC.SapAutomation.Abstractions.Storage;

public interface IJobRunStore
{
  Task RegisterAsync(string jobName, DateTimeOffset startedAt, DateTimeOffset? finishedAt = null, bool succeeded = true,
                     string? message = null, CancellationToken cancellationToken = default);
}
