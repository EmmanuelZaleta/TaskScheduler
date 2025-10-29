using System;
using System.IO;
using System.Threading;
using SAPFEWSELib;
using YCC.AnalisisCogisInventario.Config;
using YCC.AnalisisCogisInventario.Logging;
using YCC.AnalisisCogisInventario.Utilities;

namespace YCC.AnalisisCogisInventario.Services.Sap;

internal sealed class CogiExportService
{
    public string Run(GuiSession session, AppConfig cfg)
    {
        var plant = cfg.Plant;
        var exportDir = ResolveExportDirectory(cfg);
        var exportName = BuildExportFileName(cfg);
        var fullPath = Path.IsPathRooted(exportName) ? exportName : Path.Combine(exportDir, exportName);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        ConsoleLogger.Info($"Ejecutando transaccion COGI para centro {plant}...");
        Maximize(session);
        StartTransaction(session, "COGI");
        WaitUntilReady(session, 10, "Transaccion COGI");

        // Ingresar centro
        Execute("Ingresar centro", session, () =>
        {
            if (!TrySetText(session, "wnd[0]/usr/ctxtS_WERKS-LOW", plant))
                TrySetText(session, "wnd[0]/usr/txtS_WERKS-LOW", plant);
        });

        // Ejecutar
        Execute("Ejecutar consulta", session, () =>
        {
            (session.FindById("wnd[0]/tbar[1]/btn[8]", false) as GuiButton)?.Press();
        });
        WaitUntilReady(session, 15, "Resultado COGI");

        // Exportar
        Execute("Abrir dialogo de exportacion", session, () =>
        {
            (session.FindById("wnd[0]/tbar[1]/btn[20]", false) as GuiButton)?.Press();
        });
        WaitUntilReady(session, 5, "Dialogo exportacion");

        Execute("Confirmar formato de exportacion", session, () =>
        {
            (session.FindById("wnd[1]/tbar[0]/btn[0]", false) as GuiButton)?.Press();
        });
        WaitUntilReady(session, 5, "Confirmar exportacion");

        // Establecer ruta/archivo
        Execute("Especificar nombre de archivo", session, () =>
        {
            var pathOnly = Path.GetDirectoryName(fullPath)!;
            var fileOnly = Path.GetFileName(fullPath);

            var dyPath = session.FindById("wnd[1]/usr/ctxtDY_PATH", false) as GuiCTextField;
            if (dyPath != null) dyPath.Text = pathOnly;

            var dyFileC = session.FindById("wnd[1]/usr/ctxtDY_FILENAME", false) as GuiCTextField;
            var dyFileT = session.FindById("wnd[1]/usr/txtDY_FILENAME", false) as GuiTextField;
            if (dyFileC != null) dyFileC.Text = dyPath != null ? fileOnly : fullPath;
            else if (dyFileT != null) dyFileT.Text = dyPath != null ? fileOnly : fullPath;
        });

        // Guardar
        Execute("Guardar archivo", session, () =>
        {
            (session.FindById("wnd[1]/tbar[0]/btn[11]", false) as GuiButton)?.Press();
        });
        WaitUntilReady(session, 15, "Guardar archivo");

        // Confirmar sobreescritura si aparece
        DismissAnyModal(session);

        // Verificar archivo
        var ok = WaitForFile(fullPath, 20);
        if (!ok)
            ConsoleLogger.Warn($"No se pudo confirmar la creacion del archivo en '{fullPath}'. Verifica configuracion de exportacion en SAP.");
        else
            ConsoleLogger.Success($"Archivo generado: {fullPath}");

        return fullPath;
    }

    private static string ResolveExportDirectory(AppConfig cfg)
    {
        var dir = cfg.ExportDirectory;
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(AppContext.BaseDirectory, "exports");
        }
        return Path.GetFullPath(dir);
    }

    private static string BuildExportFileName(AppConfig cfg)
    {
        var pattern = string.IsNullOrWhiteSpace(cfg.ExportFileName) ? "cogi_{timestamp}.txt" : cfg.ExportFileName!;
        var name = pattern.Replace("{timestamp}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        if (string.IsNullOrWhiteSpace(Path.GetExtension(name)))
            name += ".txt";
        return name;
    }

    private static void Maximize(GuiSession session)
    {
        Execute("Maximizar ventana", session, () =>
        {
            (session.FindById("wnd[0]", false) as GuiFrameWindow)?.Maximize();
        });
    }

    private static void StartTransaction(GuiSession session, string tcode)
    {
        Execute($"Ir a transaccion {tcode}", session, () =>
        {
            bool forceOk = tcode.IndexOf('/') >= 0; // si incluye / usar OKCODE
            if (!forceOk)
            {
                try { session.StartTransaction(tcode); return; } catch { /* fallback */ }
            }

            var okcd = session.FindById("wnd[0]/tbar[0]/okcd", false) as GuiTextField
                       ?? throw new InvalidOperationException("No se encontró el campo OKCODE.");
            var wnd0 = session.FindById("wnd[0]", false) as GuiFrameWindow
                       ?? throw new InvalidOperationException("No se encontró la ventana principal.");
            try { okcd.Text = string.Empty; } catch { }
            okcd.Text = tcode;
            wnd0.SendVKey(0);
        });
    }

    private static void Execute(string desc, GuiSession session, Action action)
    {
        try
        {
            ConsoleLogger.Info($"-> {desc}...");
            action();
        }
        catch (Exception ex)
        {
            ConsoleLogger.Warn($"Error al ejecutar '{desc}': {ex.Message}");
            throw;
        }
    }

    private static void WaitUntilReady(GuiSession session, int timeoutSeconds, string context)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            bool busy = false;
            try { busy = session.Busy; } catch { }
            if (!busy)
            {
                Thread.Sleep(300);
                try { busy = session.Busy; } catch { busy = false; }
            }
            if (!busy) return;
            Thread.Sleep(300);
        }
        ConsoleLogger.Warn($"Advertencia: Timeout esperando '{context}'.");
    }

    private static bool TrySetText(GuiSession session, string id, string value)
    {
        try
        {
            var obj = session.FindById(id, false);
            if (obj == null) return false;
            switch (obj)
            {
                case GuiTextField tf: tf.Text = value; return true;
                case GuiCTextField ctf: ctf.Text = value; return true;
                default:
                    try { dynamic d = obj; d.Text = value; return true; } catch { return false; }
            }
        }
        catch { return false; }
    }

    private static bool WaitForFile(string fullPath, int seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        long lastSize = -1; int stableCount = 0;
        while (DateTime.UtcNow < until)
        {
            try
            {
                if (File.Exists(fullPath))
                {
                    var size = new FileInfo(fullPath).Length;
                    if (size == lastSize) { stableCount++; if (stableCount >= 3) return true; }
                    else { stableCount = 0; lastSize = size; }
                }
            }
            catch { }
            Thread.Sleep(300);
        }
        return File.Exists(fullPath);
    }

    private static void DismissAnyModal(GuiSession session)
    {
        try
        {
            for (int i = 1; i <= 3; i++)
            {
                try
                {
                    dynamic dialog = session.FindById($"wnd[{i}]", false);
                    if (dialog == null) continue;
                    var paths = new[]
                    {
                        $"wnd[{i}]/tbar[0]/btn[0]",
                        $"wnd[{i}]/usr/btnSPOP-OPTION1",
                        $"wnd[{i}]/usr/btnSPOP-OPTION2",
                        $"wnd[{i}]/tbar[0]/btn[12]"
                    };
                    foreach (var p in paths)
                    {
                        try { dynamic b = session.FindById(p, false); if (b != null) { b.Press(); Thread.Sleep(200); break; } } catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
