using System.ComponentModel.DataAnnotations;

namespace YCC.SapAutomation.Sap.Options;

public sealed class SapOptions
{
  public const string SectionName = "Sap";

  [Required]
  [RegularExpression("^(Gui|Rfc)$", ErrorMessage = "Sap:Mode debe ser 'Gui' o 'Rfc'.")]
  public string Mode { get; set; } = "Rfc";
}
