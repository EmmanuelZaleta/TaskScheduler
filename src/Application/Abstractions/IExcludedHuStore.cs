namespace YCC.SapAutomation.Application.Abstractions
{
  public interface IExcludedHuStore
  {
    Task<bool> IsExcludedAsync(string handlingUnit, CancellationToken cancellationToken = default);
    Task AddAsync(string handlingUnit, string reason, CancellationToken cancellationToken = default);
  }
}
