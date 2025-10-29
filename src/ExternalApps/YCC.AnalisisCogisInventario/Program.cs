using System;
using System.Runtime.Versioning;
using YCC.AnalisisCogisInventario.Config;
using YCC.AnalisisCogisInventario.Logging;
using YCC.AnalisisCogisInventario.Services.Sap;
using YCC.AnalisisCogisInventario.Services.Sql;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            ConsoleLogger.Error("Esta aplicacion requiere Windows para conectarse a SAP GUI.");
            return 1;
        }

        Console.Title = $"YCC.AnalisisCogisInventario PID={Environment.ProcessId} {DateTime.Now:HH:mm:ss}";
        ConsoleLogger.Info("Iniciando Analisis COGI de Inventario...");
        ConsoleLogger.Info($"WorkingDirectory: {Environment.CurrentDirectory}");

        try
        {
            var cfg = ConfigLoader.Load(args);
            Execute(cfg);
            ConsoleLogger.Success("Proceso completado exitosamente.");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Error: {ex.Message}");
            ConsoleLogger.Error($"StackTrace: {ex.StackTrace}");
            return 1;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Execute(AppConfig cfg)
    {
        ConsoleLogger.Info("Conectando a SAP GUI...");

        var connector = new SapConnector();
        object? app = null, conn = null, sess = null;
        try
        {
            var application = connector.GetApplication(); app = application;
            var connection = connector.EnsureConnection(application, cfg); conn = connection;
            var session = connector.EnsureSession(connection, cfg); sess = session;
            connector.EnsureLogin(session, cfg);

            // Ejecutar primero FICQ312 y luego COGI, en la MISMA sesion
            var ficq = new Ficq312ExportService();
            var ficqPath = ficq.Run(session, cfg);

            // Carga a SQL opcional para FICQ312
            try
            {
                var ficqUploader = new Ficq312SqlUploader();
                var rows = ficqUploader.Upload(ficqPath, cfg);
                if (rows > 0)
                {
                    // SP de propagacion para FICQ312
                    try { StoredProcExecutor.Execute(cfg.SqlConnection, "sp_Ficq312_propagar"); }
                    catch (Exception spEx) { ConsoleLogger.Warn($"SP FICQ312 fallo: {spEx.Message}"); }
                }
            }
            catch (Exception ex)
            {
                ConsoleLogger.Warn($"No se pudo cargar FICQ312 a SQL: {ex.Message}");
            }

            var export = new CogiExportService();
            var cogiPath = export.Run(session, cfg);

            // Carga a SQL opcional para COGI
            var uploader = new CogiSqlUploader();
            var cogiRows = uploader.Upload(cogiPath, cfg);
            if (cogiRows > 0)
            {
                // SP de propagacion para COGI
                try { StoredProcExecutor.Execute(cfg.SqlConnection, "sp_cogi_propagar"); }
                catch (Exception spEx) { ConsoleLogger.Warn($"SP COGI fallo: {spEx.Message}"); }
            }

            // Ejecutar LX03 (misma sesion) y exportar usando lista de materiales desde dbo.COGI
            try
            {
            // Ejecutar TQMR1600 (SE16N) antes de LX03
            try
            {
                var tqmr = new Tqmr1600ExportService();
                var tqmrPath = tqmr.Run(session, cfg);
                try
                {
                    var tqmrUploader = new Tqmr1600SqlUploader();
                    var tqmrRows = tqmrUploader.Upload(tqmrPath, cfg);
                    if (tqmrRows > 0)
                    {
                        // Ejecutar SP de propagación para TQMR1600
                        try { StoredProcExecutor.Execute(cfg.SqlConnection, "sp_TQMR1600_propagar"); }
                        catch (Exception spEx) { ConsoleLogger.Warn($"SP TQMR1600 fallo: {spEx.Message}"); }
                    }
                }
                catch (Exception upSe) { ConsoleLogger.Warn($"No se pudo cargar TQMR1600 a SQL: {upSe.Message}"); }
            }
            catch (Exception exSe) { ConsoleLogger.Warn($"TQMR1600 fallo: {exSe.Message}"); }

                var lx03 = new Lx03ExportService();
                var lx03Path = lx03.Run(session, cfg);

                // Cargar LX03 a SQL (RAW por linea)
                try
                {
                    var lx03Uploader = new Lx03SqlUploader();
                    var lxRows = lx03Uploader.Upload(lx03Path, cfg);
                    if (lxRows > 0)
                    {
                        // SP de propagacion para LX03 y luego el total
                        try { StoredProcExecutor.Execute(cfg.SqlConnection, "sp_lx03_propagar"); }
                        catch (Exception spEx) { ConsoleLogger.Warn($"SP LX03 fallo: {spEx.Message}"); }

                        try { StoredProcExecutor.Execute(cfg.SqlConnection, "sp_AnalisisCogisInventario_propagar_todo"); }
                        catch (Exception spEx) { ConsoleLogger.Warn($"SP global fallo: {spEx.Message}"); }
                    }
                }
                catch (Exception ex2)
                {
                    ConsoleLogger.Warn($"No se pudo cargar LX03 a SQL: {ex2.Message}");
                }
            }
            catch (Exception ex)
            {
                ConsoleLogger.Warn($"LX03 fallo: {ex.Message}");
            }
        }
        finally
        {
            try
            {
                if (cfg.CloseSessionOnExit && sess is SAPFEWSELib.GuiSession gs)
                {
                    SapSessionCloser.CloseGracefully(gs);
                }
            }
            catch { }
            TryRelease(sess);
            TryRelease(conn);
            TryRelease(app);
        }
    }

    private static void TryRelease(object? value)
    {
        if (value is null) return;
        try { if (System.Runtime.InteropServices.Marshal.IsComObject(value)) System.Runtime.InteropServices.Marshal.ReleaseComObject(value); }
        catch { }
    }
}
