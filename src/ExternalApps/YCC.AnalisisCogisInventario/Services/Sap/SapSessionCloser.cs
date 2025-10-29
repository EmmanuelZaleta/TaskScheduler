using System;
using System.Threading;
using SAPFEWSELib;
using YCC.AnalisisCogisInventario.Logging;

namespace YCC.AnalisisCogisInventario.Services.Sap;

internal static class SapSessionCloser
{
    public static void CloseGracefully(GuiSession? session, int timeoutSeconds = 10)
    {
        if (session == null) return;
        try
        {
            ConsoleLogger.Info("Cerrando sesion SAP utilizada...");
            TryCloseWnd0(session);
            // Intentar confirmar modales comunes (Si/No, Cancelar)
            var until = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < until)
            {
                bool anyModal = false;
                for (int i = 1; i <= 3; i++)
                {
                    try
                    {
                        dynamic dialog = session.FindById($"wnd[{i}]", false);
                        if (dialog == null) continue;
                        anyModal = true;
                        // botones tipicos: Aceptar/Si/Continuar/No/Cancelar
                        var btns = new[] { "tbar[0]/btn[0]", "usr/btnSPOP-OPTION1", "usr/btnSPOP-OPTION2", "tbar[0]/btn[12]" };
                        foreach (var b in btns)
                        {
                            try { dynamic btn = session.FindById($"wnd[{i}]/{b}", false); if (btn != null) { btn.Press(); Thread.Sleep(200); break; } } catch { }
                        }
                    }
                    catch { }
                }
                if (!anyModal) break;
                Thread.Sleep(200);
            }
        }
        catch (Exception ex)
        {
            ConsoleLogger.Warn($"Fallo al cerrar sesion: {ex.Message}");
        }
    }

    private static void TryCloseWnd0(GuiSession session)
    {
        try
        {
            var wnd0 = session.FindById("wnd[0]", false) as GuiFrameWindow;
            if (wnd0 != null)
            {
                try { wnd0.Close(); Thread.Sleep(200); }
                catch { try { wnd0.SendVKey(12); Thread.Sleep(200); } catch { } }
            }
        }
        catch { }
    }
}

