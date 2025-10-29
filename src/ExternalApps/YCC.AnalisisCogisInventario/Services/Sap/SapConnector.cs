using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using SAPFEWSELib;
using YCC.AnalisisCogisInventario.Config;
using YCC.AnalisisCogisInventario.Logging;
using YCC.AnalisisCogisInventario.Utilities;

namespace YCC.AnalisisCogisInventario.Services.Sap;

internal sealed class SapConnector
{
    public GuiApplication GetApplication()
    {
        // 1) Intento preferente: SapROTWr.SapROTWrapper -> GetROTEntry("SAPGUI") -> GetScriptingEngine
        try
        {
            var rotType = Type.GetTypeFromProgID("SapROTWr.SapROTWrapper");
            if (rotType != null)
            {
                object? rotWrapper = null;
                object? rotEntry = null;
                try
                {
                    rotWrapper = Activator.CreateInstance(rotType);
                    rotEntry = rotWrapper?.GetType().InvokeMember(
                        "GetROTEntry",
                        BindingFlags.InvokeMethod,
                        binder: null,
                        target: rotWrapper,
                        args: new object?[] { "SAPGUI" });

                    var app = rotEntry?.GetType().InvokeMember(
                        "GetScriptingEngine",
                        BindingFlags.InvokeMethod,
                        binder: null,
                        target: rotEntry,
                        args: null) as GuiApplication;

                    if (app != null)
                    {
                        ConsoleLogger.Info("SAP GUI encontrado via SapROTWr.SapROTWrapper (ROT).");
                        return app;
                    }
                }
                finally
                {
                    TryRelease(rotEntry);
                    TryRelease(rotWrapper);
                }
            }
        }
        catch (COMException ex)
        {
            ConsoleLogger.Warn($"ROT SapROTWr fallo: {ex.Message}");
        }

        // 2) Alternativa: objeto activo "SAPGUI" (algunos entornos lo registran)
        try
        {
            var rot = ComRot.TryGetActiveObject("SAPGUI");
            if (rot != null)
            {
                var app = rot.GetType().InvokeMember(
                    "GetScriptingEngine",
                    BindingFlags.InvokeMethod,
                    binder: null,
                    target: rot,
                    args: null) as GuiApplication;
                if (app != null)
                {
                    ConsoleLogger.Info("SAP GUI encontrado via ROT objeto activo.");
                    return app;
                }
            }
        }
        catch (COMException ex)
        {
            ConsoleLogger.Warn($"No se pudo obtener 'SAPGUI' del ROT activo: {ex.Message}");
        }

        var type = Type.GetTypeFromProgID("Sapgui.ScriptingCtrl.1");
        if (type == null)
            throw new InvalidOperationException("No se encontro 'Sapgui.ScriptingCtrl.1'. Verifica instalacion y scripting.");

        object? ctrl = null;
        try
        {
            ctrl = Activator.CreateInstance(type);
            var engine = ctrl!.GetType().InvokeMember(
                "GetScriptingEngine",
                BindingFlags.InvokeMethod, null, ctrl, null);
            var app2 = engine as GuiApplication
                       ?? throw new InvalidOperationException("No se pudo crear GuiApplication desde ScriptingCtrl");
            ConsoleLogger.Info("SAP GUI encontrado via ScriptingCtrl.");
            return app2;
        }
        finally { TryRelease(ctrl); }
    }

    public GuiConnection EnsureConnection(GuiApplication app, AppConfig cfg)
    {
        // 1) Reusar una conexion existente si la hay
        if (app.Connections.Count > 0)
        {
            // Preferir por nombre configurado
            var named = FindConnectionByName(app, cfg.ConnectionName);
            if (named != null)
            {
                ConsoleLogger.Info($"Reusando conexion existente: {Safe(() => named.Description)}");
                return named;
            }

            // Si no hay nombre, preferir una con usuario logueado
            var logged = FindLoggedInConnection(app);
            if (logged != null)
            {
                ConsoleLogger.Info($"Reusando conexion abierta (logueada): {Safe(() => logged.Description)}");
                return logged;
            }

            // En ultimo caso, la primera
            var first = app.Connections.ElementAt(0) as GuiConnection
                        ?? throw new InvalidOperationException("No se pudo obtener la conexion SAP.");
            ConsoleLogger.Info($"Reusando primera conexion disponible: {Safe(() => first.Description)}");
            return first;
        }

        // 2) Si no hay conexiones, abrir una nueva segun config
        TryOpenConnection(app, cfg);
        Utilities.Waiter.WaitFor(() => app.Connections.Count > 0, 40);
        if (app.Connections.Count == 0)
            throw new InvalidOperationException("No hay conexiones SAP abiertas.");

        var connection = app.Connections.ElementAt(0) as GuiConnection
                         ?? throw new InvalidOperationException("No se pudo obtener la conexion SAP.");
        ConsoleLogger.Info($"Conexion creada: {Safe(() => connection.Description)}");
        return connection;
    }

    private static GuiConnection? FindConnectionByName(GuiApplication app, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try
        {
            for (int i = 0; i < app.Connections.Count; i++)
            {
                var c = app.Connections.ElementAt(i) as GuiConnection;
                if (c == null) continue;
                var desc = Safe(() => c.Description) ?? string.Empty;
                if (desc.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    desc.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return c;
                }
            }
        }
        catch { }
        return null;
    }

    private static GuiConnection? FindLoggedInConnection(GuiApplication app)
    {
        try
        {
            for (int i = 0; i < app.Connections.Count; i++)
            {
                var c = app.Connections.ElementAt(i) as GuiConnection;
                if (c == null) continue;
                try
                {
                    for (int s = 0; s < c.Children.Count; s++)
                    {
                        var sess = c.Children.ElementAt(s) as GuiSession;
                        if (sess == null) continue;
                        var user = sess.Info?.User;
                        if (!string.IsNullOrWhiteSpace(user)) return c;
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    public GuiSession EnsureSession(GuiConnection connection, AppConfig cfg)
    {
        // Si se pide abrir una nueva sesion, intentar crearla sobre la conexion actual
        if (cfg.OpenNewSession)
        {
            var before = connection.Children.Count;
            bool created = false;
            // 1) Preferir crear desde una sesion existente (equivalente a VBScript: session.createSession)
            if (before > 0)
            {
                var baseSession = connection.Children.ElementAt(0) as GuiSession;
                if (baseSession != null)
                {
                    // Maximizar como en tu script y crear
                    Try(() => (connection.FindById("wnd[0]", false) as GuiFrameWindow)?.Maximize());
                    created = TryCreateNewSessionOnSession(baseSession);
                }
            }
            // 2) Fallback: crear desde la conexion (CreateSession)
            if (!created)
            {
                created = TryCreateNewSession(connection);
            }
            if (created)
            {
                var ok = Utilities.Waiter.WaitFor(() => connection.Children.Count > before, 8);
                if (ok)
                {
                    var newSession = connection.Children.ElementAt(before) as GuiSession;
                    if (newSession != null)
                    {
                        ConsoleLogger.Info("Nueva sesion creada sobre la conexion actual.");
                        ConsoleLogger.Info($"Usuario: {Safe(() => newSession.Info.User) ?? "desconocido"}");
                        return newSession;
                    }
                }
                ConsoleLogger.Warn("No se pudo localizar la nueva sesion creada; continuando con sesion existente.");
            }
            else
            {
                ConsoleLogger.Warn("No fue posible crear una nueva sesion (limite alcanzado o politica del sistema). Se usara una existente.");
            }
        }

        // Reusar sesion existente de esta conexion si hay
        if (connection.Children.Count > 0)
        {
            var session = connection.Children.ElementAt(0) as GuiSession
                          ?? throw new InvalidOperationException("No se pudo obtener la sesion SAP.");
            ConsoleLogger.Info($"Sesion reutilizada. Usuario: {Safe(() => session.Info.User) ?? "desconocido"}");
            return session;
        }

        // Si sigue sin sesiones, intentar reutilizar una sesion de otra conexion ya abierta
        try
        {
            var app = connection.Parent as GuiApplication;
            if (app != null && app.Connections.Count > 0)
            {
                for (int i = 0; i < app.Connections.Count; i++)
                {
                    var c = app.Connections.ElementAt(i) as GuiConnection;
                    if (c == null || c.Children.Count == 0) continue;
                    var s = c.Children.ElementAt(0) as GuiSession;
                    if (s != null)
                    {
                        ConsoleLogger.Info($"Reutilizando sesion existente de otra conexion: {Safe(() => c.Description)}");
                        ConsoleLogger.Info($"Usuario: {Safe(() => s.Info.User) ?? "desconocido"}");
                        return s;
                    }
                }
            }
        }
        catch { }
        throw new InvalidOperationException("No hay sesiones activas en la conexion SAP.");
    }

    private static bool TryCreateNewSession(GuiConnection connection)
    {
        try
        {
            // Evitar depender del interop en tiempo de compilacion
            connection.GetType().InvokeMember(
                "CreateSession",
                BindingFlags.InvokeMethod,
                binder: null,
                target: connection,
                args: null);
            return true;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateNewSessionOnSession(GuiSession session)
    {
        try
        {
            session.GetType().InvokeMember(
                "CreateSession",
                BindingFlags.InvokeMethod,
                binder: null,
                target: session,
                args: null);
            return true;
        }
        catch (TargetInvocationException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void EnsureLogin(GuiSession session, AppConfig cfg)
    {
        // Si ya hay usuario autenticado, no intentes loguear
        if (!string.IsNullOrWhiteSpace(Safe(() => session.Info.User))) return;

        // Evitar auto-login si no estamos en la pantalla de login
        if (!IsLoginScreen(session))
        {
            // Dar unos segundos para que Info.User se propague
            var propagated = Utilities.Waiter.WaitFor(() =>
            {
                try { return !string.IsNullOrWhiteSpace(session.Info.User); } catch { return false; }
            }, 5);
            if (propagated) return;
        }

        // Solo intentar auto-login si realmente estamos en la pantalla de login y hay credenciales
        if (IsLoginScreen(session) &&
            !string.IsNullOrWhiteSpace(cfg.Username) && !string.IsNullOrWhiteSpace(cfg.Password))
        {
            TryAutoLogin(session, cfg.Username!, cfg.Password!);
        }

        ConsoleLogger.Info($"Esperando autenticacion de usuario... (timeout {cfg.LoginTimeoutSeconds}s)");
        var ok = Utilities.Waiter.WaitFor(() =>
        {
            try { return !string.IsNullOrWhiteSpace(session.Info.User); } catch { return false; }
        }, cfg.LoginTimeoutSeconds);
        if (!ok) throw new InvalidOperationException("No se detecto autenticacion de usuario en el tiempo esperado.");

        ConsoleLogger.Info($"Usuario autenticado: {Safe(() => session.Info.User) ?? "desconocido"}");
    }

    private static void TryOpenConnection(GuiApplication app, AppConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.ConnectionString))
        {
            ConsoleLogger.Info("Abriendo conexion via connectionString...");
            Try(() => app.OpenConnectionByConnectionString(cfg.ConnectionString, true));
            return;
        }

        if (!string.IsNullOrWhiteSpace(cfg.ConnectionName))
        {
            ConsoleLogger.Info($"Abriendo conexion '{cfg.ConnectionName}'...");
            Try(() => app.OpenConnection(cfg.ConnectionName, true));
            if (app.Connections.Count > 0) return;

            var alt = SapLandscape.TryResolveConnStringFromLandscape(cfg.ConnectionName!);
            if (!string.IsNullOrWhiteSpace(alt))
            {
                ConsoleLogger.Info("Reintentando via connectionString del landscape...");
                Try(() => app.OpenConnectionByConnectionString(alt!, true));
                return;
            }
        }

        ConsoleLogger.Info("Iniciando saplogon.exe...");
        TryStartSapLogon();

        var pick = SapLandscape.PickPreferredEntry(SapLandscape.ListEntries());
        if (!string.IsNullOrWhiteSpace(pick))
        {
            ConsoleLogger.Info($"Intentando abrir entrada '{pick}' desde landscape...");
            Try(() => app.OpenConnection(pick!, true));
        }
    }

    private static void TryAutoLogin(GuiSession session, string user, string pass)
    {
        Try(() =>
        {
            var userField = session.FindById("wnd[0]/usr/txtRSYST-BNAME", false) as GuiTextField;
            var passField = session.FindById("wnd[0]/usr/pwdRSYST-BCODE", false) as GuiPasswordField;
            var wnd0 = session.FindById("wnd[0]", false) as GuiMainWindow;
            if (userField != null && wnd0 != null)
            {
                userField.Text = user;
                if (passField != null)
                {
                    passField.Text = pass;
                    wnd0.SendVKey(0);
                }
            }
        });
    }

    private static bool IsLoginScreen(GuiSession session)
    {
        try
        {
            var userField = session.FindById("wnd[0]/usr/txtRSYST-BNAME", false);
            var passField = session.FindById("wnd[0]/usr/pwdRSYST-BCODE", false);
            return userField != null || passField != null;
        }
        catch { return false; }
    }

    private static void TryStartSapLogon()
    {
        Try(() =>
        {
            var p = new System.Diagnostics.ProcessStartInfo("saplogon.exe")
            {
                UseShellExecute = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized
            };
            System.Diagnostics.Process.Start(p);
        });
    }

    private static void Try(Action action)
    {
        try { action(); } catch { }
    }

    private static string? Safe(Func<string> f)
    {
        try { return f(); } catch { return null; }
    }

    private static void TryRelease(object? o)
    {
        if (o == null) return;
        try { if (Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); } catch { }
    }
}

internal static class SapLandscape
{
    public static string? TryResolveConnStringFromLandscape(string entryName)
    {
        try
        {
            foreach (var f in LandscapeFiles())
            {
                try
                {
                    var xml = System.Xml.Linq.XDocument.Load(f);
                    var entries = xml.Descendants("Entry")
                        .Select(e => new
                        {
                            Name = (string?)e.Attribute("name"),
                            ConnStr = (string?)e.Attribute("connectionString") ?? (string?)e.Element("ConnectionString")
                        })
                        .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                        .ToList();

                    var exact = entries.FirstOrDefault(x => string.Equals(x.Name, entryName, StringComparison.Ordinal));
                    if (exact != null && !string.IsNullOrWhiteSpace(exact.ConnStr))
                        return exact.ConnStr;

                    var partial = entries.FirstOrDefault(x => x.Name!.IndexOf(entryName, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (partial != null && !string.IsNullOrWhiteSpace(partial.ConnStr))
                        return partial.ConnStr;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    public static string[] ListEntries()
    {
        try
        {
            return LandscapeFiles()
                .SelectMany(f =>
                {
                    var x = System.Xml.Linq.XDocument.Load(f);
                    return x.Descendants("Entry")
                        .Select(e => (string?)e.Attribute("name"))
                        .Where(n => !string.IsNullOrWhiteSpace(n))!;
                })
                .Distinct()
                .ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    public static string? PickPreferredEntry(string[] entries)
    {
        if (entries == null || entries.Length == 0) return null;
        if (entries.Length == 1) return entries[0];
        string[] order = new[] { "PRD", "QAS", "CALIDAD", "QA", "DEV", "DES" };
        foreach (var key in order)
        {
            var found = entries.FirstOrDefault(e => e.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(found)) return found;
        }
        return entries[0];
    }

    private static string[] LandscapeFiles()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var common = Path.Combine(appData, "SAP", "Common");
        return new[]
        {
            Path.Combine(common, "SAPUILandscape.xml"),
            Path.Combine(common, "SAPUILandscapeGlobal.xml")
        }.Where(File.Exists).ToArray();
    }
}
