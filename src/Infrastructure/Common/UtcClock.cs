using YCC.SapAutomation.Domain.Common;

namespace YCC.SapAutomation.Infrastructure.Common
{
  public sealed class UtcClock : IClock
  {
    public DateTime UtcNow => DateTime.UtcNow;
  }
}
