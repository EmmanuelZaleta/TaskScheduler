using YCC.SapAutomation.Domain.Tqmbulk;

namespace YCC.SapAutomation.Abstractions.Tqmbulk;

public interface ITqmbulkSource
{
  Task<TqmbulkBatch> FetchAsync(CancellationToken cancellationToken = default);
}
