using Microsoft.Extensions.Options;
using YCC.SapAutomation.Application.Abstractions;
using YCC.SapAutomation.Application.Options;
using YCC.SapAutomation.Domain.Tqmbulk;

namespace YCC.SapAutomation.Sap.Adapters
{
  /// <summary>
  /// Implementacion dirigida a un agente externo (IPC) que opera via SAP GUI en sesion interactiva.
  /// </summary>
  public sealed class SapGuiAgentTqmbulkSource : ITqmbulkSource
  {
    private readonly SapOptions _options;

    public SapGuiAgentTqmbulkSource(IOptions<SapOptions> options) => _options = options.Value;

    public Task<TqmbulkBatch> FetchAsync(CancellationToken cancellationToken = default)
    {
      // TODO: Implementar comunicacion con el agente (Named Pipes/gRPC) y mapear resultados.
      return Task.FromResult(new TqmbulkBatch(Array.Empty<TqmbulkEntry>()));
    }
  }
}
