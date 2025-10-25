namespace YCC.SapAutomation.Domain.Tqmbulk
{
  public sealed class TqmbulkBatch
  {
    public IReadOnlyCollection<TqmbulkEntry> Entries { get; }

    public TqmbulkBatch(IReadOnlyCollection<TqmbulkEntry> entries) =>
      Entries = entries;
  }
}
