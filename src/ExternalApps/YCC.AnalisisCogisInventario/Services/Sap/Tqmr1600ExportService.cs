using System;
using System.IO;
using System.Threading;
using SAPFEWSELib;
using YCC.AnalisisCogisInventario.Config;
using YCC.AnalisisCogisInventario.Logging;

namespace YCC.AnalisisCogisInventario.Services.Sap;

internal sealed class Tqmr1600ExportService
{
    public string Run(GuiSession session, AppConfig cfg)
    {
        // Prefer TQMR1600* keys; fallback to SE16N*
        var plant = !string.IsNullOrWhiteSpace(cfg.Tqmr1600Plant) ? cfg.Tqmr1600Plant! : (!string.IsNullOrWhiteSpace(cfg.Se16nPlant) ? cfg.Se16nPlant! : "1815");
        var date = !string.IsNullOrWhiteSpace(cfg.Tqmr1600Date) ? cfg.Tqmr1600Date! : (!string.IsNullOrWhiteSpace(cfg.Se16nDate) ? cfg.Se16nDate! : DateTime.Today.AddDays(-1).ToString("MM/dd/yyyy"));
        var exportDir = ResolveExportDirectory(cfg);
        var exportName = !string.IsNullOrWhiteSpace(cfg.Tqmr1600ExportFileName) ? cfg.Tqmr1600ExportFileName! : (string.IsNullOrWhiteSpace(cfg.Se16nExportFileName) ? "TQMR1600.txt" : cfg.Se16nExportFileName!);
        var fullPath = Path.Combine(exportDir, exportName);

        try { Directory.CreateDirectory(exportDir); } catch { }

        ConsoleLogger.Info($"Ejecutando TQMR1600 export (plant={plant}, date={date})...");
        Maximize(session);

        // Navegación usando helpers, como en los primeros servicios
        //Execute("Ir a /n", session, () => { TryOkCode(session, "/n"); });
        //Execute("Ir a SE16N", session, () => { TryOkCode(session, "SE16N"); });

        // Establecer tabla GD-TAB, por defecto /YZKNA/TQMR1600, y Enter
        Execute("Establecer tabla TQMR1600", session, () =>
        {
            var okcdObj = session.FindById("wnd[0]/tbar[0]/okcd", false)
                        ?? session.FindById("wnd[0]/usr/txtOKCODE", false);
            if (okcdObj == null) throw new InvalidOperationException("No se encontró el campo OKCODE.");

            ((dynamic)okcdObj).text = "/n";              // tal cual lo envías (ej. "/n/YZKNA/FICQ312")
            try { ((dynamic)okcdObj).setFocus(); } catch { }
            var wnd0 = session.FindById("wnd[0]", false) as GuiFrameWindow
                       ?? throw new InvalidOperationException("No se encontró la ventana principal wnd[0].");
            wnd0.SendVKey(0); // Enter
            ((dynamic)okcdObj).text = "SE16N";              // tal cual lo envías (ej. "/n/YZKNA/FICQ312")
            try { ((dynamic)okcdObj).setFocus(); } catch { }
            wnd0 = session.FindById("wnd[0]", false) as GuiFrameWindow
                      ?? throw new InvalidOperationException("No se encontró la ventana principal wnd[0].");
            wnd0.SendVKey(0); // Enter

            var tableName = string.IsNullOrWhiteSpace(cfg.Tqmr1600TableName) ? "/YZKNA/TQMR1600" : cfg.Tqmr1600TableName!;
            var gd = session.FindById("wnd[0]/usr/ctxtGD-TAB", false) as GuiCTextField;
            if (gd != null) { gd.Text = tableName; TrySendEnter(session); }
            else { TrySendEnter(session); }
        });

        // Establecer filtros en la tabla de campos (robusto, con reintentos)
        Execute("Establecer plant en tabla de campos", session, () =>
        {
            EnsureSelfieldsTableVisible(session);
            var cell = TryFind(session, "wnd[0]/usr/tblSAPLSE16NSELFIELDS_TC/ctxtGS_SELFIELDS-LOW[2,1]")
                       as GuiCTextField
                    ?? TryFind(session, "wnd[0]/usr/tblSAPLSE16NSELFIELDS_TC/ctxtGS_SELFIELDS-LOW[1,1]") as GuiCTextField
                    ?? TryFind(session, "wnd[0]/usr/tblSAPLSE16NSELFIELDS_TC/ctxtGS_SELFIELDS-LOW[3,1]") as GuiCTextField;
            if (cell != null)
            {
                cell.Text = plant; try { cell.SetFocus(); cell.CaretPosition = Math.Min(plant.Length, 4); } catch { }
                TrySendEnter(session);
            }
            else
            {
                ConsoleLogger.Warn("No se encontró la celda de Plant; se continúa sin setearla.");
            }
        });

        Execute("Scroll a fila de fecha", session, () =>
        {
            try { dynamic tbl = session.FindById("wnd[0]/usr/tblSAPLSE16NSELFIELDS_TC", false); tbl.verticalScrollbar.position = 1; } catch { }
        });

        Execute("Establecer fecha", session, () =>
        {
            EnsureSelfieldsTableVisible(session);
            var dateCell = TryFind(session, "wnd[0]/usr/tblSAPLSE16NSELFIELDS_TC/ctxtGS_SELFIELDS-LOW[2,11]") as GuiCTextField
                        ?? TryFind(session, "wnd[0]/usr/tblSAPLSE16NSELFIELDS_TC/ctxtGS_SELFIELDS-LOW[1,11]") as GuiCTextField;
            if (dateCell != null)
            {
                dateCell.Text = date; try { dateCell.SetFocus(); dateCell.CaretPosition = date.Length; } catch { }
            }
            else
            {
                ConsoleLogger.Warn("No se encontró la celda de fecha; se continúa sin setearla.");
            }
        });

        // Ejecutar
        Execute("Ejecutar consulta", session, () => { (session.FindById("wnd[0]/tbar[1]/btn[8]", false) as GuiButton)?.Press(); });
        WaitUntilReady(session, 10, "Resultado SE16N");

        // Exportar
        Execute("Exportar a PC", session, () =>
        {
            dynamic grid = session.FindById("wnd[0]/usr/cntlRESULT_LIST/shellcont/shell", false);
            grid.PressToolbarContextButton("&MB_EXPORT"); Thread.Sleep(200);
            grid.SelectContextMenuItem("&PC");
        });
        WaitUntilReady(session, 5, "Dialogo exportacion SE16N");

        Execute("Confirmar formato", session, () => { (session.FindById("wnd[1]/tbar[0]/btn[0]", false) as GuiButton)?.Press(); });
        WaitUntilReady(session, 5, "Confirmar formato SE16N");

        Execute("Establecer ruta y archivo", session, () =>
        {
            var dyPath = session.FindById("wnd[1]/usr/ctxtDY_PATH", false) as GuiCTextField;
            if (dyPath != null) { dyPath.SetFocus(); try { dyPath.CaretPosition = exportDir.Length; } catch { } dyPath.Text = exportDir; }
            var dyFileC = session.FindById("wnd[1]/usr/ctxtDY_FILENAME", false) as GuiCTextField;
            var dyFileT = dyFileC == null ? session.FindById("wnd[1]/usr/txtDY_FILENAME", false) as GuiTextField : null;
            if (dyFileC != null) { try { dyFileC.Text = exportName; dyFileC.CaretPosition = Math.Min(8, exportName.Length); } catch { } }
            else if (dyFileT != null) { try { dyFileT.Text = exportName; dyFileT.CaretPosition = Math.Min(8, exportName.Length); } catch { } }
        });
        Execute("Guardar archivo", session, () => { (session.FindById("wnd[1]/tbar[0]/btn[11]", false) as GuiButton)?.Press(); });
        WaitUntilReady(session, 10, "Guardar SE16N");

        // Salida a /n
        TryOkCode(session, "/n");

        if (WaitForFile(fullPath, 15)) ConsoleLogger.Success($"Archivo TQMR1600 generado: {fullPath}");
        else ConsoleLogger.Warn($"No se pudo confirmar archivo TQMR1600 en '{fullPath}'.");
        return fullPath;
    }

    private static void Maximize(GuiSession session) { try { (session.FindById("wnd[0]", false) as GuiFrameWindow)?.Maximize(); } catch { } }
    private static void TrySendEnter(GuiSession session) { try { (session.FindById("wnd[0]", false) as GuiFrameWindow)?.SendVKey(0); } catch { } }
    private static object? TryFind(GuiSession session, string id) { try { return session.FindById(id, false); } catch { return null; } }
    private static void EnsureSelfieldsTableVisible(GuiSession session)
    {
        // Intentar localizar la tabla de selfields y dar un pequeño tiempo para que aparezca
        var until = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < until)
        {
            var tbl = TryFind(session, "wnd[0]/usr/tblSAPLSE16NSELFIELDS_TC");
            if (tbl != null) return;
            Thread.Sleep(200);
        }
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
                ok.Text = cmd; wnd0.SendVKey(0); Thread.Sleep(200);
            }
        }
        catch { }
    }

    private static string ResolveExportDirectory(AppConfig cfg)
    {
        var dir = cfg.ExportDirectory;
        if (string.IsNullOrWhiteSpace(dir)) dir = Path.GetFullPath(@"C:\Users\90022817\OneDrive - Yazaki\Documents\");
        return dir;
    }

    private static void Execute(string desc, GuiSession session, Action action)
    {
        try { ConsoleLogger.Info($"-> {desc}..."); action(); }
        catch (Exception ex) { ConsoleLogger.Warn($"Error en '{desc}': {ex.Message}"); throw; }
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
            Thread.Sleep(250);
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
}
