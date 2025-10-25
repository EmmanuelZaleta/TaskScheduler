namespace YCC.SapAutomation.Domain.Tqmbulk
{
  public sealed class TqmbulkEntry
  {
    public string HandlingUnit { get; }
    public string StationNumber { get; }
    public DateOnly RegistrationDate { get; }
    public TimeOnly RegistrationTime { get; }
    public string? Material { get; }
    public string? ManufacturingLine { get; }

    public TqmbulkEntry(
      string handlingUnit,
      string stationNumber,
      DateOnly registrationDate,
      TimeOnly registrationTime,
      string? material,
      string? manufacturingLine)
    {
      HandlingUnit = handlingUnit;
      StationNumber = stationNumber;
      RegistrationDate = registrationDate;
      RegistrationTime = registrationTime;
      Material = material;
      ManufacturingLine = manufacturingLine;
    }
  }
}
