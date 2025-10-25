using YCC.SapAutomation.Domain.Tqmbulk;

namespace YCC.SapAutomation.Abstractions.Tqmbulk;

public interface ITqmbulkRepository
{
  Task SaveAsync(TqmbulkBatch batch, CancellationToken cancellationToken = default);
}
