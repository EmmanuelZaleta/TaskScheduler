using Microsoft.Extensions.Options;
using YCC.SapAutomation.Abstractions.Tqmbulk;
using YCC.SapAutomation.Domain.Tqmbulk;
using YCC.SapAutomation.Sap.Options;

namespace YCC.SapAutomation.Sap.Adapters
{
  /// <summary>
  /// Implementacion basada en SAP RFC (SAP .NET Connector). Actualmente retorna datos vacios como stub.
  /// </summary>
  public sealed class SapRfcTqmbulkSource : ITqmbulkSource
  {
    private readonly SapOptions _options;

    public SapRfcTqmbulkSource(IOptions<SapOptions> options) => _options = options.Value;

    public Task<TqmbulkBatch> FetchAsync(CancellationToken cancellationToken = default)
    {
      // TODO: Implementar llamada real via SAP NCo.
      return Task.FromResult(new TqmbulkBatch(Array.Empty<TqmbulkEntry>()));
    }
  }
}
