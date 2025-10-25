using System.ComponentModel.DataAnnotations;

namespace YCC.SapAutomation.Sap.Options;

public sealed class SapOptions
{
  public const string SectionName = "Sap";

  [Required]
  [RegularExpression("^(Gui|Rfc)$", ErrorMessage = "Sap:Mode debe ser 'Gui' o 'Rfc'.")]
  public string Mode { get; set; } = "Rfc";

  public SapGuiOptions Gui { get; set; } = new();

  public SapRfcOptions Rfc { get; set; } = new();
}

public sealed class SapGuiOptions
{
  public bool BootstrapEnabled { get; set; } = true;

  [Required(AllowEmptyStrings = false)]
  public string BootstrapOperationCode { get; set; } = "SAP_BOOTSTRAP";

  [Required(AllowEmptyStrings = false)]
  public string ProcessName { get; set; } = "saplogon";

  [Range(10, 86400)]
  public int MonitorIntervalSeconds { get; set; } = 60;

  public string? SystemId { get; set; }
  public string? Client { get; set; }
  public string? User { get; set; }
  public string? Password { get; set; }
  public string? Lang { get; set; }
}

public sealed class SapRfcOptions
{
  public string? Ashost { get; set; }
  public string? Sysnr { get; set; }
  public string? Client { get; set; }
  public string? User { get; set; }
  public string? Password { get; set; }
}
