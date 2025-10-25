namespace YCC.SapAutomation.Abstractions.Automation;

public interface IAutomationManifestProvider
{
  Task<IReadOnlyCollection<AutomationManifest>> LoadAsync(CancellationToken cancellationToken = default);
}
