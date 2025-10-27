using Microsoft.Win32;
using System.Diagnostics;

namespace YCC.SapAutomation.Host.Utilities;

public static class StartupHelper
{
  private const string AppName = "YCC.JobHost";
  private const string RunKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run";

  public static bool RegisterInWindowsStartup()
  {
    try
    {
      var exePath = Process.GetCurrentProcess().MainModule?.FileName;
      if (string.IsNullOrEmpty(exePath))
        return false;

      Registry.SetValue(RunKey, AppName, $"\"{exePath}\"", RegistryValueKind.String);
      return true;
    }
    catch
    {
      return false;
    }
  }

  public static bool UnregisterFromWindowsStartup()
  {
    try
    {
      using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
      key?.DeleteValue(AppName, throwOnMissingValue: false);
      return true;
    }
    catch
    {
      return false;
    }
  }

  public static bool IsRegisteredInStartup()
  {
    try
    {
      var value = Registry.GetValue(RunKey, AppName, null);
      return value != null;
    }
    catch
    {
      return false;
    }
  }
}
