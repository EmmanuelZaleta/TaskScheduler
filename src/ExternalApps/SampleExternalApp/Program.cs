using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
internal static class Program
{
  public static int Main(string[] args)
  {
    var parsed = ParseArgs(args);

    if (!string.IsNullOrWhiteSpace(parsed.Title))
      Console.Title = parsed.Title;
    else
      Console.Title = $"SampleExternalApp PID={Environment.ProcessId} {DateTime.Now:HH:mm:ss}";

    var greeting = Environment.GetEnvironmentVariable("SAMPLE_EXTERNAL_GREETING") ?? "Hola";
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"{Timestamp()} {greeting} desde SampleExternalApp.");
    Console.ResetColor();

    Console.WriteLine($"{Timestamp()} WorkingDirectory: {Environment.CurrentDirectory}");
    Console.WriteLine($"{Timestamp()} Args: {string.Join(' ', args)}");

    if (parsed.Sap.Enabled)
    {
      try
      {
        EnsureSapLogin(parsed.Sap);
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"{Timestamp()} Error al iniciar SAP GUI: {ex.Message}");
        return 1;
      }
    }
    else
    {
      Console.WriteLine($"{Timestamp()} Login a SAP omitido (--no-sap).");
    }

    if (parsed.SleepSeconds > 0)
    {
      Console.WriteLine($"{Timestamp()} Durmiendo {parsed.SleepSeconds}s...");
      Thread.Sleep(TimeSpan.FromSeconds(parsed.SleepSeconds));
    }

    if (!string.IsNullOrWhiteSpace(parsed.WritePath))
    {
      try
      {
        var path = Path.GetFullPath(parsed.WritePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, $"{DateTime.Now:O} Emmanuel Zaleta Escribio esta linea {Environment.NewLine}");
        Console.WriteLine($"{Timestamp()} Escribi en: {path}");
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"{Timestamp()} Error escribiendo archivo: {ex.Message}");
      }
    }

    if (parsed.HoldSeconds > 0)
    {
      Console.WriteLine($"{Timestamp()} Manteniendo ventana {parsed.HoldSeconds}s...");
      Thread.Sleep(TimeSpan.FromSeconds(parsed.HoldSeconds));
    }

    if (parsed.ExitCode != 0)
    {
      Console.WriteLine($"{Timestamp()} Saliendo con codigo {parsed.ExitCode}");
      return parsed.ExitCode;
    }

    Console.WriteLine($"{Timestamp()} Listo (exit 0)");
    return 0;
  }

  private static ParsedArgs ParseArgs(string[] args)
  {
    int sleep = 0; string write = string.Empty; int exit = 0; int hold = 0; string? title = null;
    bool sapEnabled = !IsDisabled(Environment.GetEnvironmentVariable("SAMPLE_EXTERNAL_SKIP_SAP"));
    string sapSystem = Environment.GetEnvironmentVariable("SAMPLE_EXTERNAL_SAP_SYSTEM") ?? ".YNCA - EQ2 - ERP QA2";
    string sapClient = Environment.GetEnvironmentVariable("SAMPLE_EXTERNAL_SAP_CLIENT") ?? string.Empty;
    string sapUser = Environment.GetEnvironmentVariable("SAMPLE_EXTERNAL_SAP_USER") ?? "90022817";
    string sapPassword = Environment.GetEnvironmentVariable("SAMPLE_EXTERNAL_SAP_PASSWORD") ?? "Yazaki202512345";
    string sapLang = Environment.GetEnvironmentVariable("SAMPLE_EXTERNAL_SAP_LANG") ?? "ES";
    string? sapGuiPath = Environment.GetEnvironmentVariable("SAMPLE_EXTERNAL_SAP_GUI_EXE");
    int sapTimeoutSeconds = TryParseInt(Environment.GetEnvironmentVariable("SAMPLE_EXTERNAL_SAP_TIMEOUT_SECONDS"), 45);

    for (int i = 0; i < args.Length; i++)
    {
      var a = args[i];
      if (a.Equals("--sleep", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i+1], out var s))
      { sleep = s; i++; continue; }
      if (a.Equals("--write", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
      { write = args[i+1]; i++; continue; }
      if (a.Equals("--exit", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i+1], out var e))
      { exit = e; i++; continue; }
      if (a.Equals("--hold", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i+1], out var h))
      { hold = h; i++; continue; }
      if (a.Equals("--title", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
      { title = args[i+1]; i++; continue; }
      if (a.Equals("--no-sap", StringComparison.OrdinalIgnoreCase) || a.Equals("--sap-skip", StringComparison.OrdinalIgnoreCase))
      { sapEnabled = false; continue; }
      if (a.Equals("--sap", StringComparison.OrdinalIgnoreCase))
      { sapEnabled = true; continue; }
      if (a.Equals("--sap-system", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
      { sapSystem = args[i+1]; i++; continue; }
      if (a.Equals("--sap-client", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
      { sapClient = args[i+1]; i++; continue; }
      if (a.Equals("--sap-user", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
      { sapUser = args[i+1]; i++; continue; }
      if (a.Equals("--sap-password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
      { sapPassword = args[i+1]; i++; continue; }
      if (a.Equals("--sap-lang", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
      { sapLang = args[i+1]; i++; continue; }
      if (a.Equals("--sap-gui", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
      { sapGuiPath = args[i+1]; i++; continue; }
      if (a.Equals("--sap-timeout", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i+1], out var timeout))
      { sapTimeoutSeconds = timeout; i++; continue; }
    }

    var sapOptions = new SapLoginOptions(
      sapEnabled,
      sapSystem,
      sapClient,
      sapUser,
      sapPassword,
      sapLang,
      sapGuiPath,
      sapTimeoutSeconds < 5 ? 5 : sapTimeoutSeconds);

    return new ParsedArgs(sleep, write, exit, hold, title, sapOptions);
  }

  private static void EnsureSapLogin(SapLoginOptions options)
  {
    Console.WriteLine($"{Timestamp()} Preparando login en SAP GUI para \"{options.SystemDescription}\" como usuario {options.User}...");

    object? rotEntry = null;
    dynamic application = null;
    dynamic connection = null;
    dynamic session = null;

    try
    {
      rotEntry = AcquireSapRotEntry(options);
      application = GetSapGuiApplication(rotEntry);
      connection = EnsureConnection(application, options);
      session = EnsureSession(connection, options);

      var currentUser = TryGet(() => (string?)session.Info.User);
      if (!string.IsNullOrWhiteSpace(currentUser) && currentUser.Equals(options.User, StringComparison.OrdinalIgnoreCase))
      {
        Console.WriteLine($"{Timestamp()} La sesion SAP ya se encuentra autenticada como {currentUser}.");
        return;
      }

      PopulateLoginFields(session, options);
      session.findById("wnd[0]").sendVKey(0);

      WaitForUser(session, options);
      Console.WriteLine($"{Timestamp()} Login SAP completado.");
    }
    finally
    {
      ReleaseComObject((object?)session);
      ReleaseComObject((object?)connection);
      ReleaseComObject((object?)application);
      ReleaseComObject(rotEntry);
    }
  }

  private static object AcquireSapRotEntry(SapLoginOptions options)
  {
    var rotType = Type.GetTypeFromProgID("SapROTWr.SapROTWrapper");
    if (rotType is null)
      throw new InvalidOperationException("No se encontro la libreria SapROTWr. Instala SAP GUI con scripting habilitado.");

    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(options.StartupTimeoutSeconds);
    var launched = false;

    while (DateTime.UtcNow < deadline)
    {
      var entry = TryGetSapRotEntry(rotType);
      if (entry is not null)
        return entry;

      if (!launched)
      {
        LaunchSapGui(options);
        launched = true;
      }

      Thread.Sleep(TimeSpan.FromSeconds(1));
    }

    throw new InvalidOperationException("SAP GUI no se registro en el tiempo limite configurado.");
  }

  private static object? TryGetSapRotEntry(Type rotType)
  {
    object? rot = null;
    try
    {
      rot = Activator.CreateInstance(rotType);
      if (rot is null)
        return null;

      return ((dynamic)rot).GetROTEntry("SAPGUI");
    }
    finally
    {
      ReleaseComObject(rot);
    }
  }

  private static void LaunchSapGui(SapLoginOptions options)
  {
    var exe = !string.IsNullOrWhiteSpace(options.SapGuiPath)
      ? options.SapGuiPath
      : Environment.GetEnvironmentVariable("SAP_GUI_EXE") ?? "saplogon.exe";

    Console.WriteLine($"{Timestamp()} Iniciando SAP GUI con \"{exe}\"...");

    try
    {
      var info = new ProcessStartInfo(exe)
      {
        UseShellExecute = true
      };
      Process.Start(info);
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException($"No se pudo iniciar SAP GUI (\"{exe}\"). {ex.Message}", ex);
    }
  }

  private static dynamic GetSapGuiApplication(object rotEntry)
    => rotEntry
      .GetType()
      .InvokeMember("GetScriptingEngine", BindingFlags.InvokeMethod, binder: null, target: rotEntry, args: null);

  private static dynamic EnsureConnection(dynamic application, SapLoginOptions options)
  {
    var existing = TryGetExistingConnection(application, options.SystemDescription);
    if (existing is not null)
    {
      Console.WriteLine($"{Timestamp()} Reutilizando conexion existente: {options.SystemDescription}.");
      return existing;
    }

    Console.WriteLine($"{Timestamp()} Abriendo conexion hacia {options.SystemDescription}...");
    return application.OpenConnection(options.SystemDescription, true);
  }

  private static dynamic? TryGetExistingConnection(dynamic application, string systemDescription)
  {
    try
    {
      int count = Convert.ToInt32(application.Children.Count);
      for (int i = 0; i < count; i++)
      {
        dynamic connection = application.Children(i);
        var description = TryGet(() => (string?)connection.Description) ?? string.Empty;
        var name = TryGet(() => (string?)connection.Name) ?? string.Empty;
        if (MatchesSystem(description, systemDescription) || MatchesSystem(name, systemDescription))
          return connection;
      }
    }
    catch
    {
      // ignored - enumerating conexiones puede fallar si SAP GUI no esta listo.
    }

    return null;
  }

  private static bool MatchesSystem(string actual, string expected)
    => !string.IsNullOrWhiteSpace(actual) &&
       actual.Trim().Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase);

  private static dynamic EnsureSession(dynamic connection, SapLoginOptions options)
  {
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(options.StartupTimeoutSeconds);
    while (DateTime.UtcNow < deadline)
    {
      try
      {
        if (Convert.ToInt32(connection.Children.Count) > 0)
          return connection.Children(0);
      }
      catch
      {
        // Esperamos a que la sesion aparezca.
      }

      Thread.Sleep(TimeSpan.FromMilliseconds(500));
    }

    throw new InvalidOperationException("No se encontro una sesion SAP activa en el tiempo limite.");
  }

  private static void PopulateLoginFields(dynamic session, SapLoginOptions options)
  {
    if (!string.IsNullOrWhiteSpace(options.Client))
      TrySetText(session, "wnd[0]/usr/txtRSYST-MANDT", options.Client);

    TrySetText(session, "wnd[0]/usr/txtRSYST-BNAME", options.User);
    TrySetText(session, "wnd[0]/usr/pwdRSYST-BCODE", options.Password, mask: true);

    if (!string.IsNullOrWhiteSpace(options.Language))
      TrySetText(session, "wnd[0]/usr/txtRSYST-LANGU", options.Language);
  }

  private static void WaitForUser(dynamic session, SapLoginOptions options)
  {
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(options.StartupTimeoutSeconds);
    while (DateTime.UtcNow < deadline)
    {
      var currentUser = TryGet(() => (string?)session.Info.User);
      if (!string.IsNullOrWhiteSpace(currentUser) && currentUser.Equals(options.User, StringComparison.OrdinalIgnoreCase))
        return;

      HandleOptionalPopup(session);

      Thread.Sleep(TimeSpan.FromMilliseconds(500));
    }

    Console.Error.WriteLine($"{Timestamp()} Advertencia: no se pudo confirmar el usuario autenticado en SAP dentro del tiempo limite.");
  }

  private static void HandleOptionalPopup(dynamic session)
  {
    try
    {
      var popup = session.findById("wnd[1]");
      var text = TryGet(() => (string?)popup.Text) ?? string.Empty;
      if (!string.IsNullOrEmpty(text))
      {
        // Aceptamos mensajes comunes como "This system is not a production system".
        popup.sendVKey(0);
      }
    }
    catch
    {
      // No hay popup.
    }
  }

  private static void TrySetText(dynamic session, string id, string value, bool mask = false)
  {
    try
    {
      var control = session.findById(id);
      control.Text = value;
      if (!mask)
        Console.WriteLine($"{Timestamp()} Escribiendo en {id}: {value}");
      else
        Console.WriteLine($"{Timestamp()} Escribiendo en {id}: ****");
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"{Timestamp()} No se pudo escribir en {id}: {ex.Message}");
    }
  }

  private static void ReleaseComObject(object? value)
  {
    if (value is null)
      return;

    try
    {
      if (Marshal.IsComObject(value))
        Marshal.ReleaseComObject(value);
    }
    catch
    {
      // Ignorado: best-effort.
    }
  }

  private static int TryParseInt(string? value, int fallback)
    => int.TryParse(value, out var result) ? result : fallback;

  private static bool IsDisabled(string? value)
    => !string.IsNullOrWhiteSpace(value) &&
       (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("yes", StringComparison.OrdinalIgnoreCase));

  private static T? TryGet<T>(Func<T> getter)
  {
    try
    {
      return getter();
    }
    catch
    {
      return default;
    }
  }

  private static string Timestamp() => $"[{DateTime.Now:HH:mm:ss}]";

  private sealed record ParsedArgs(
    int SleepSeconds,
    string WritePath,
    int ExitCode,
    int HoldSeconds,
    string? Title,
    SapLoginOptions Sap);

  private sealed record SapLoginOptions(
    bool Enabled,
    string SystemDescription,
    string Client,
    string User,
    string Password,
    string Language,
    string? SapGuiPath,
    int StartupTimeoutSeconds);
}
