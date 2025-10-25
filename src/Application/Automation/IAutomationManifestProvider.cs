namespace YCC.SapAutomation.Application.Automation
{
  public interface IAutomationManifestProvider
  {
    Task<IReadOnlyCollection<AutomationManifest>> LoadAsync(CancellationToken cancellationToken = default);
  }
}
