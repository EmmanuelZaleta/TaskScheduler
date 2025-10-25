namespace YCC.SapAutomation.Abstractions.Storage;

public interface IExcludedHuStore
{
  Task<IReadOnlySet<string>> LoadAsync(CancellationToken cancellationToken = default);
  Task<bool> IsExcludedAsync(string handlingUnit, CancellationToken cancellationToken = default);
  Task AddAsync(string handlingUnit, string reason, CancellationToken cancellationToken = default);
}
