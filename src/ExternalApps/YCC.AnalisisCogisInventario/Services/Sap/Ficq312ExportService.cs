using System;
using System.IO;
using System.Threading;
using SAPFEWSELib;
using YCC.AnalisisCogisInventario.Config;
using YCC.AnalisisCogisInventario.Logging;

namespace YCC.AnalisisCogisInventario.Services.Sap;

internal sealed class Ficq312ExportService
{
    public string Run(GuiSession session, AppConfig cfg)
    {
        var plant = cfg.Plant;
        var (fromDate, toDate) = ResolveDateRange(cfg);
        var exportDir = ResolveExportDirectory(cfg);
        var exportName = BuildExportFileName(cfg); // p.ej. "FICQ312.txt"
        var fullPath = Path.IsPathRooted(exportName) ? exportName : Path.Combine(exportDir, exportName);

        // Asegurar carpeta local si se va a usar
        try { Directory.CreateDirectory(exportDir); } catch { /* tolerante */ }

        ConsoleLogger.Info($"Ejecutando {cfg.Ficq312TCode} para centro {plant}, rango {fromDate} - {toDate}...");
        Maximize(session);

        // === Navegación EXACTA con OKCODE via FindById (sin sanitizar) ===
        Execute("Ir a transacción (OKCODE exacto)", session, () =>
        {
            var okcdObj = session.FindById("wnd[0]/tbar[0]/okcd", false)
                          ?? session.FindById("wnd[0]/usr/txtOKCODE", false);
            if (okcdObj == null) throw new InvalidOperationException("No se encontró el campo OKCODE.");

            ((dynamic)okcdObj).text = cfg.Ficq312TCode;              // tal cual lo envías (ej. "/n/YZKNA/FICQ312")
            try { ((dynamic)okcdObj).setFocus(); } catch { }
            try { ((dynamic)okcdObj).caretPosition = cfg.Ficq312TCode?.Length ?? 0; } catch { }

            var wnd0 = session.FindById("wnd[0]", false) as GuiFrameWindow
                       ?? throw new InvalidOperationException("No se encontró la ventana principal wnd[0].");
            wnd0.SendVKey(0); // Enter
        });
        WaitUntilReady(session, 8, cfg.Ficq312TCode);

        // Ingresar filtros
        Execute("Ingresar parámetros FICQ312", session, () =>
        {
            var pPlantC = session.FindById("wnd[0]/usr/ctxtSP$00001-LOW", false) as GuiCTextField;
            var pPlantT = pPlantC == null ? session.FindById("wnd[0]/usr/txtSP$00001-LOW", false) as GuiTextField : null;
            if (pPlantC != null) pPlantC.Text = plant; else if (pPlantT != null) pPlantT.Text = plant;

            var dLowC = session.FindById("wnd[0]/usr/ctxtSP$00002-LOW", false) as GuiCTextField;
            var dLowT = dLowC == null ? session.FindById("wnd[0]/usr/txtSP$00002-LOW", false) as GuiTextField : null;
            var dHighC = session.FindById("wnd[0]/usr/ctxtSP$00002-HIGH", false) as GuiCTextField;
            var dHighT = dHighC == null ? session.FindById("wnd[0]/usr/txtSP$00002-HIGH", false) as GuiTextField : null;

            if (dLowC != null) dLowC.Text = fromDate; else if (dLowT != null) dLowT.Text = fromDate;
            if (dHighC != null) dHighC.Text = toDate; else if (dHighT != null) dHighT.Text = toDate;

            // Enfocar HIGH como en el script
            try
            {
                if (dHighC != null) { dHighC.SetFocus(); dHighC.CaretPosition = Math.Min(5, toDate.Length); }
                else if (dHighT != null) { dHighT.SetFocus(); dHighT.CaretPosition = Math.Min(5, toDate.Length); }
            }
            catch { }
        });

        // Ejecutar
        Execute("Ejecutar consulta FICQ312", session, () =>
        {
            (session.FindById("wnd[0]/tbar[1]/btn[8]", false) as GuiButton)?.Press();
        });
        WaitUntilReady(session, 12, "Resultado FICQ312");

        // Esperar a que el grid cargue completamente los datos antes de exportar
        ConsoleLogger.Info("-> Esperando a que los datos se carguen en el grid...");
        Thread.Sleep(3000);

        // Resolver grid de resultados
        dynamic resultGrid = null;
        try { resultGrid = session.FindById("wnd[0]/usr/cntlCONTAINER/shellcont/shell", false); } catch { }
        if (resultGrid == null) resultGrid = FindResultGrid(session, timeoutSeconds: 20);
        if (resultGrid == null)
        {
            Thread.Sleep(1000);
            resultGrid = FindResultGrid(session, timeoutSeconds: 5);
        }
        if (resultGrid == null)
            throw new InvalidOperationException("No se encontró el grid de resultados para exportar.");

        // Ordenar DESC por toolbar (opcional, según tu script)
        Execute("Ordenar descendente", session, () =>
        {
            try { ((dynamic)resultGrid).PressToolbarButton("&SORT_DSC"); } catch { return; }
        });
        // Confirmar pop-up (si aparece)
        Execute("Confirmar ordenamiento", session, () =>
        {
            try { ((dynamic)session.FindById("wnd[1]/usr/subSUB_DYN0500:SAPLSKBH:0610/btnAPP_FL_SING", false))?.Press(); } catch { }
            try { (session.FindById("wnd[1]/tbar[0]/btn[0]", false) as GuiButton)?.Press(); } catch { }
        });

        // Exportar desde contenedor
        Execute("Exportar a PC", session, () =>
        {
            try { ((dynamic)resultGrid).PressToolbarContextButton("&MB_EXPORT"); Thread.Sleep(250); } catch { }
            try { ((dynamic)resultGrid).SelectContextMenuItem("&PC"); } catch { }
        });
        WaitUntilReady(session, 5, "Dialogo exportacion FICQ312");

        // Confirmar formato de exportación
        Execute("Confirmar formato exportacion", session, () =>
        {
            (session.FindById("wnd[1]/tbar[0]/btn[0]", false) as GuiButton)?.Press();
        });
        WaitUntilReady(session, 5, "Confirmar exportacion FICQ312");

        // Establecer nombre de archivo (y ruta si el dialogo lo permite)
        Execute("Establecer archivo FICQ312", session, () =>
        {
            var dyPath = session.FindById("wnd[1]/usr/ctxtDY_PATH", false) as GuiCTextField;
            if (dyPath != null)
            {
                try { dyPath.Text = exportDir; } catch { }
            }

            var dyFileC = session.FindById("wnd[1]/usr/ctxtDY_FILENAME", false) as GuiCTextField;
            var dyFileT = session.FindById("wnd[1]/usr/txtDY_FILENAME", false) as GuiTextField;
            if (dyFileC != null) { dyFileC.Text = exportName; TrySetCaret(dyFileC, 0); }
            else if (dyFileT != null) { dyFileT.Text = exportName; TrySetCaret(dyFileT, 0); }
        });

        Execute("Guardar archivo FICQ312", session, () =>
        {
            (session.FindById("wnd[1]/tbar[0]/btn[11]", false) as GuiButton)?.Press();
        });
        WaitUntilReady(session, 15, "Guardar archivo FICQ312");

        DismissAnyModal(session);

        // Intentar confirmar archivo en la ruta configurada
        if (WaitForFile(fullPath, 20))
            ConsoleLogger.Success($"Archivo FICQ312 generado: {fullPath}");
        else
            ConsoleLogger.Warn($"No se pudo confirmar el archivo en '{fullPath}'. Verifica la carpeta por defecto de SAP GUI.");

        return fullPath;
        WaitUntilReady(session, 5, "Diálogo exportación FICQ312");

        Execute("Confirmar formato exportación", session, () =>
        {
            (session.FindById("wnd[1]/tbar[0]/btn[0]", false) as GuiButton)?.Press();
        });
        WaitUntilReady(session, 5, "Confirmar exportación FICQ312");

        // Establecer ruta/archivo
        Execute("Establecer ruta y nombre de archivo FICQ312", session, () =>
        {
            var dyPathC = session.FindById("wnd[1]/usr/ctxtDY_PATH", false) as GuiCTextField;
            var dyPathT = dyPathC == null ? session.FindById("wnd[1]/usr/txtDY_PATH", false) as GuiTextField : null;

            var dyFileC = session.FindById("wnd[1]/usr/ctxtDY_FILENAME", false) as GuiCTextField;
            var dyFileT = dyFileC == null ? session.FindById("wnd[1]/usr/txtDY_FILENAME", false) as GuiTextField : null;

            bool hasPathField = (dyPathC != null) || (dyPathT != null);
            var fileOnly = Path.GetFileName(fullPath);

            if (hasPathField)
            {
                if (dyPathC != null) dyPathC.Text = exportDir; else if (dyPathT != null) dyPathT.Text = exportDir;
                if (dyFileC != null) { dyFileC.Text = fileOnly; TrySetCaret(dyFileC, 0); }
                else if (dyFileT != null) { dyFileT.Text = fileOnly; TrySetCaret(dyFileT, 0); }
            }
            else
            {
                // Algunos diálogos aceptan la ruta completa en DY_FILENAME
                if (dyFileC != null) { dyFileC.Text = fullPath; TrySetCaret(dyFileC, 0); }
                else if (dyFileT != null) { dyFileT.Text = fullPath; TrySetCaret(dyFileT, 0); }
            }
        });

        Execute("Guardar archivo FICQ312", session, () =>
        {
            (session.FindById("wnd[1]/tbar[0]/btn[11]", false) as GuiButton)?.Press();
        });
        WaitUntilReady(session, 15, "Guardar archivo FICQ312");

        DismissAnyModal(session);

        // Validación local (si el diálogo usó nuestra ruta)
        if (WaitForFile(fullPath, 10))
            ConsoleLogger.Success($"Export FICQ312 guardado como: {fullPath}");
        else
            ConsoleLogger.Success($"Export FICQ312 solicitado como: {exportName} (posible ubicación por defecto de SAP GUI)");

        return fullPath;
    }

    private static dynamic FindResultGrid(GuiSession session, int timeoutSeconds)
    {
        var until = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        string[] ids =
        {
            "wnd[0]/usr/cntlCONTAINER/shellcont/shell",
            "wnd[0]/usr/cntlGRID1/shellcont/shell",
            "wnd[0]/usr/cntlALV_CONTAINER/shellcont/shell",
            "wnd[0]/usr/cntlCUSTOM/shellcont/shell",
            "wnd[0]/usr/shell/shellcont/shell"
        };
        while (DateTime.UtcNow < until)
        {
            foreach (var id in ids)
            {
                try
                {
                    dynamic grid = session.FindById(id, false);
                    if (grid != null) return grid;
                }
                catch { }
            }
            Thread.Sleep(250);
        }
        return null;
    }

    private static (string fromDate, string toDate) ResolveDateRange(AppConfig cfg)
    {
        const string fmt = "MM/dd/yyyy";
        if (!string.IsNullOrWhiteSpace(cfg.Ficq312From) && !string.IsNullOrWhiteSpace(cfg.Ficq312To))
            return (cfg.Ficq312From!, cfg.Ficq312To!);

        var to = DateTime.Today;
        var from = to.AddDays(-Math.Max(1, cfg.Ficq312DaysBack));
        return (from.ToString(fmt), to.ToString(fmt));
    }

    private static string BuildExportFileName(AppConfig cfg)
    {
        return string.IsNullOrWhiteSpace(cfg.Ficq312ExportFileName)
            ? "FICQ312.txt"
            : cfg.Ficq312ExportFileName!;
    }

    private static void Maximize(GuiSession session)
    {
        Execute("Maximizar ventana", session, () =>
        {
            (session.FindById("wnd[0]", false) as GuiFrameWindow)?.Maximize();
        });
    }

    private static void TrySetCaret(object field, int pos)
    {
        try { dynamic d = field; d.CaretPosition = pos; d.SetFocus(); } catch { }
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
        ConsoleLogger.Warn($"Advertencia: Timeout esperando '{context}'.");
    }

    private static bool WaitForFile(string fullPath, int seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        long last = -1; int stable = 0;
        while (DateTime.UtcNow < until)
        {
            try
            {
                if (File.Exists(fullPath))
                {
                    var len = new FileInfo(fullPath).Length;
                    if (len == last) { stable++; if (stable >= 3) return true; }
                    else { stable = 0; last = len; }
                }
            }
            catch { }
            Thread.Sleep(250);
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
                        try
                        {
                            dynamic b = session.FindById(p, false);
                            if (b != null) { b.Press(); Thread.Sleep(150); break; }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static string ResolveExportDirectory(AppConfig cfg)
    {
        var dir = cfg.ExportDirectory;
        if (string.IsNullOrWhiteSpace(dir))
            dir = Path.Combine(AppContext.BaseDirectory, "exports");
        return Path.GetFullPath(dir);
    }
}
