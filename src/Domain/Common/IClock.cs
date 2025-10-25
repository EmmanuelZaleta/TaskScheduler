namespace YCC.SapAutomation.Domain.Common
{
  public interface IClock
  {
    DateTime UtcNow { get; }
  }
}
