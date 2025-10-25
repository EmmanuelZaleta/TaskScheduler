using System.Collections.Generic;

namespace YCC.SapAutomation.Application.Jobs.ExternalProcess;

/// <summary>
/// Representa la configuracion necesaria para lanzar un proceso externo.
/// </summary>
public sealed record ExternalProcessCommand(
  string Command,
  string Arguments,
  string WorkingDirectory,
  bool ShowWindow,
  IReadOnlyDictionary<string, string>? Environment);
