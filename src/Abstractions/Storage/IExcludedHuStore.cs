namespace YCC.SapAutomation.Abstractions.Storage;

public interface IExcludedHuStore
{
  Task<IReadOnlySet<string>> LoadAsync(CancellationToken cancellationToken = default);
}
