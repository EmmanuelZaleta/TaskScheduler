namespace YCC.SapAutomation.Application.Options
{
  public sealed class SapOptions
  {
    public const string SectionName = "Sap";

    public string Mode { get; set; } = "Rfc";
    public RfcOptions Rfc { get; set; } = new();
    public GuiOptions Gui { get; set; } = new();

    public sealed class RfcOptions
    {
      public string Ashost { get; set; } = string.Empty;
      public string Sysnr { get; set; } = "00";
      public string Client { get; set; } = string.Empty;
      public string User { get; set; } = string.Empty;
      public string Password { get; set; } = string.Empty;
    }

    public sealed class GuiOptions
    {
      public string SystemId { get; set; } = string.Empty;
      public string Client { get; set; } = string.Empty;
      public string User { get; set; } = string.Empty;
      public string Password { get; set; } = string.Empty;
      public string Lang { get; set; } = "EN";
    }
  }
}
