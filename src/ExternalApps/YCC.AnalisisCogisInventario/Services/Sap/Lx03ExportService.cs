using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Threading;
using SAPFEWSELib;
using YCC.AnalisisCogisInventario.Config;
using YCC.AnalisisCogisInventario.Logging;
using YCC.AnalisisCogisInventario.Utilities;

namespace YCC.AnalisisCogisInventario.Services.Sap;

internal sealed class Lx03ExportService
{
    public string Run(GuiSession session, AppConfig cfg)
    {
        var warehouse = string.IsNullOrWhiteSpace(cfg.Lx03Warehouse) ? "181" : cfg.Lx03Warehouse!;
        // Path: usa ExportDirectory si existe; si no, default del script del usuario
        var exportDir = !string.IsNullOrWhiteSpace(cfg.ExportDirectory)
            ? ResolveExportDirectory(cfg)
            : Path.GetFullPath(@"C:\Users\90022817\OneDrive - Yazaki\Documents\");
        var exportName = string.IsNullOrWhiteSpace(cfg.Lx03ExportFileName) ? "LX03.txt" : cfg.Lx03ExportFileName!;
        var fullPath = Path.Combine(exportDir, exportName);

        try { Directory.CreateDirectory(exportDir); } catch { }

        ConsoleLogger.Info("Ejecutando LX03 (segun script)...");
        Maximize(session);

        // /n y lx03 (en minusculas como en el script)
        //TryOkCode(session, "/n");
        //TryOkCode(session, "lx03");
        //WaitUntilReady(session, 5, "LX03");

        // S1_LGNUM = warehouse y caretPosition = 2, Enter
        Execute(session, "Establecer S1_LGNUM", () =>
        {
            var okcdObj = session.FindById("wnd[0]/tbar[0]/okcd", false)
                         ?? session.FindById("wnd[0]/usr/txtOKCODE", false);
            if (okcdObj == null) throw new InvalidOperationException("No se encontró el campo OKCODE.");

            ((dynamic)okcdObj).text = "/n";              // tal cual lo envías (ej. "/n/YZKNA/FICQ312")
            try { ((dynamic)okcdObj).setFocus(); } catch { }
            var wnd0 = session.FindById("wnd[0]", false) as GuiFrameWindow
                       ?? throw new InvalidOperationException("No se encontró la ventana principal wnd[0].");
            wnd0.SendVKey(0); // Enter

            ((dynamic)okcdObj).text = "lx03";              // tal cual lo envías (ej. "/n/YZKNA/FICQ312")
            try { ((dynamic)okcdObj).setFocus(); } catch { }
             wnd0 = session.FindById("wnd[0]", false) as GuiFrameWindow
                       ?? throw new InvalidOperationException("No se encontró la ventana principal wnd[0].");
            wnd0.SendVKey(0); // Enter

            var lgC = session.FindById("wnd[0]/usr/ctxtS1_LGNUM", false) as GuiCTextField;
            var lgT = lgC == null ? session.FindById("wnd[0]/usr/txtS1_LGNUM", false) as GuiTextField : null;
            var lg = (object?)lgC ?? (object?)lgT;
            if (lg == null) throw new InvalidOperationException("No se encontro el campo S1_LGNUM");
            dynamic dlg = lg!;
            dlg.Text = warehouse;
            try { dlg.CaretPosition = Math.Min(2, warehouse.Length); } catch { }
            (session.FindById("wnd[0]", false) as GuiFrameWindow)!.SendVKey(0);
        });

        // PMITB seleccionado y foco
        Execute(session, "Activar PMITB", () =>
        {
            var chk = session.FindById("wnd[0]/usr/chkPMITB", false) as GuiCheckBox;
            if (chk != null) { chk.Selected = true; try { chk.SetFocus(); } catch { } }
        });

        // Ejecutar (btn[8]) y exportar (btn[9])
        Execute(session, "Ejecutar", () => { (session.FindById("wnd[0]/tbar[1]/btn[8]", false) as GuiButton)?.Press(); });
        WaitUntilReady(session, 8, "Resultado LX03");
        Execute(session, "Abrir exportacion", () => { (session.FindById("wnd[0]/tbar[1]/btn[9]", false) as GuiButton)?.Press(); });
        Execute(session, "Confirmar formato", () => { (session.FindById("wnd[1]/tbar[0]/btn[0]", false) as GuiButton)?.Press(); });

        // DY_PATH y DY_FILENAME, caretPosition=8 para nombre
        Execute(session, "Establecer ruta y nombre", () =>
        {
            var dyPath = session.FindById("wnd[1]/usr/ctxtDY_PATH", false) as GuiCTextField;
            if (dyPath != null) dyPath.Text = exportDir;
            var dyFileC = session.FindById("wnd[1]/usr/ctxtDY_FILENAME", false) as GuiCTextField;
            var dyFileT = dyFileC == null ? session.FindById("wnd[1]/usr/txtDY_FILENAME", false) as GuiTextField : null;
            if (dyFileC != null) { dynamic d = dyFileC; d.Text = exportName; try { d.CaretPosition = Math.Min(8, exportName.Length); } catch { } }
            else if (dyFileT != null) { dynamic d = dyFileT; d.Text = exportName; try { d.CaretPosition = Math.Min(8, exportName.Length); } catch { } }
        (session.FindById("wnd[1]/tbar[0]/btn[11]", false) as GuiButton)?.Press();
        });
        Task.Delay(5000).Wait();

        // Confirmar archivo
        if (WaitForFile(fullPath, 15)) ConsoleLogger.Success($"Archivo LX03 generado: {fullPath}");
        else ConsoleLogger.Warn($"No se pudo confirmar archivo LX03 en '{fullPath}'.");

        return fullPath;
    }

    private static List<string> GetMaterialsFromCogi(AppConfig cfg)
    {
        var list = new List<string>();
        try
        {
            if (string.IsNullOrWhiteSpace(cfg.SqlConnection)) return list;
            using var cn = new SqlConnection(cfg.SqlConnection);
            cn.Open();
            using var cmd = new SqlCommand("SELECT DISTINCT Material FROM dbo.COGI WHERE Material IS NOT NULL AND LTRIM(RTRIM(Material)) <> ''", cn);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var m = (rd[0] as string)?.Trim();
                if (!string.IsNullOrWhiteSpace(m)) list.Add(m);
            }
        }
        catch (Exception ex)
        {
            ConsoleLogger.Warn($"No se pudieron leer materiales de dbo.COGI: {ex.Message}");
        }
        return list;
    }

    private static void TryOkCode(GuiSession session, string cmd)
    {
        try
        {
            var ok = session.FindById("wnd[0]/tbar[0]/okcd", false) as GuiTextField
                     ?? session.FindById("wnd[0]/usr/txtOKCODE", false) as GuiTextField;
            var wnd0 = session.FindById("wnd[0]", false) as GuiFrameWindow;
            if (ok != null && wnd0 != null)
            {
                try { ok.Text = string.Empty; } catch { }
                ok.Text = cmd;
                wnd0.SendVKey(0);
                Thread.Sleep(200);
            }
        }
        catch { }
    }

    private static void Maximize(GuiSession session)
    {
        try { (session.FindById("wnd[0]", false) as GuiFrameWindow)?.Maximize(); } catch { }
    }

    private static void Execute(GuiSession session, string step, Action act)
    {
        try { ConsoleLogger.Info($"-> {step}..."); act(); }
        catch (Exception ex) { ConsoleLogger.Warn($"Error en '{step}': {ex.Message}"); throw; }
    }

    private static void WaitUntilReady(GuiSession session, int timeoutSeconds, string context)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            bool busy = false; try { busy = session.Busy; } catch { }
            if (!busy)
            {
                Thread.Sleep(250);
                try { busy = session.Busy; } catch { busy = false; }
            }
            if (!busy) return;
            Thread.Sleep(200);
        }
        ConsoleLogger.Warn($"Advertencia: Timeout esperando '{context}'");
    }

    private static bool WaitForFile(string fullPath, int seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            try { if (File.Exists(fullPath)) return true; } catch { }
            Thread.Sleep(300);
        }
        return File.Exists(fullPath);
    }

    private static string ResolveExportDirectory(AppConfig cfg)
    {
        var dir = cfg.ExportDirectory;
        if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine(AppContext.BaseDirectory, "exports");
        return Path.GetFullPath(dir);
    }
}
