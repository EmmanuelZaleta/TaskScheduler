using YCC.SapAutomation.Domain.Tqmbulk;

namespace YCC.SapAutomation.Application.Abstractions
{
  public interface ITqmbulkSource
  {
    Task<TqmbulkBatch> FetchAsync(CancellationToken cancellationToken = default);
  }
}
