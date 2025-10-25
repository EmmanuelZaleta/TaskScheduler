using YCC.SapAutomation.Domain.Tqmbulk;

namespace YCC.SapAutomation.Application.Abstractions
{
  public interface ITqmbulkRepository
  {
    Task UpsertAsync(IEnumerable<TqmbulkEntry> entries, CancellationToken cancellationToken = default);
  }
}
