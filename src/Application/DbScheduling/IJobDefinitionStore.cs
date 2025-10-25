namespace YCC.SapAutomation.Application.DbScheduling;

public interface IJobDefinitionStore
{
  Task<IReadOnlyCollection<JobDefinition>> LoadEnabledAsync(CancellationToken cancellationToken);
}

