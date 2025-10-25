namespace YCC.SapAutomation.Abstractions.DbScheduling;

public interface IJobDefinitionStore
{
  Task<IReadOnlyCollection<JobDefinition>> LoadEnabledAsync(CancellationToken cancellationToken);
}
