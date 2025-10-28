using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using SAPFEWSELib;

// P/Invoke para GetActiveObject (no disponible en .NET 8)
internal static class NativeMethods
{
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid pclsid);

    [DllImport("ole32.dll", PreserveSig = true)]
    private static extern int GetActiveObject(in Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    [SupportedOSPlatform("windows")]
    public static object GetActiveObject(string progId)
    {
        int hr = CLSIDFromProgID(progId, out Guid clsid);
        if (hr < 0)
        {
            throw new COMException($"No se pudo obtener CLSID para ProgID: {progId}", hr);
        }

        hr = GetActiveObject(in clsid, IntPtr.Zero, out object obj);
        if (hr < 0)
        {
            throw new COMException($"No se pudo obtener instancia activa de: {progId}", hr);
        }

        return obj;
    }
}

internal static class Program
{
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine($"{Timestamp()} Esta aplicacion requiere Windows para conectarse a SAP GUI.");
            return 1;
        }

        Console.Title = $"YCC.AnalisisCogisInventario PID={Environment.ProcessId} {DateTime.Now:HH:mm:ss}";

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"{Timestamp()} Iniciando Analisis COGI de Inventario...");
        Console.ResetColor();

        Console.WriteLine($"{Timestamp()} WorkingDirectory: {Environment.CurrentDirectory}");

        try
        {
            ExecuteCogiScript();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{Timestamp()} Proceso completado exitosamente.");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"{Timestamp()} Error: {ex.Message}");
            Console.Error.WriteLine($"{Timestamp()} StackTrace: {ex.StackTrace}");
            Console.ResetColor();
            return 1;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ExecuteCogiScript()
    {
        Console.WriteLine($"{Timestamp()} Conectando a SAP GUI...");

        GuiApplication? application = null;
        GuiConnection? connection = null;
        GuiSession? session = null;

        try
        {
            application = GetSapApplication();

            if (application.Children.Count == 0)
            {
                throw new InvalidOperationException("No hay conexiones SAP abiertas. Por favor, inicie sesion en SAP primero.");
            }

            connection = application.Children.ElementAt(0) as GuiConnection
                ?? throw new InvalidOperationException("No se pudo obtener la conexion SAP.");

            Console.WriteLine($"{Timestamp()} Conexion encontrada: {TryGet(() => connection.Description) ?? "N/A"}");

            if (connection.Children.Count == 0)
            {
                throw new InvalidOperationException("No hay sesiones activas en la conexion SAP.");
            }

            session = connection.Children.ElementAt(0) as GuiSession
                ?? throw new InvalidOperationException("No se pudo obtener la sesion SAP.");

            Console.WriteLine($"{Timestamp()} Sesion encontrada. Usuario: {TryGet(() => session.Info.User) ?? "desconocido"}");

            RunCogiExport(session);
        }
        finally
        {
            ReleaseComObject(session);
            ReleaseComObject(connection);
            ReleaseComObject(application);
        }
    }

    [SupportedOSPlatform("windows")]
    private static GuiApplication GetSapApplication()
    {
        var sapGuiAuto = GetObject("SAPGUI") as GuiApplication;
        if (sapGuiAuto == null)
        {
            throw new InvalidOperationException("No se pudo conectar a SAP GUI. Asegurese de que SAP GUI este abierto.");
        }

        var app = sapGuiAuto.GetScriptingEngine() as GuiApplication;
        if (app == null)
        {
            throw new InvalidOperationException("No se pudo obtener el motor de scripting de SAP GUI.");
        }

        Console.WriteLine($"{Timestamp()} SAP GUI encontrado.");
        return app;
    }

    [SupportedOSPlatform("windows")]
    private static void RunCogiExport(GuiSession session)
    {
        const string transaction = "COGI";
        const string plant = "1815";
        const string exportFile = "cogi.txt";

        Console.WriteLine($"{Timestamp()} Ejecutando transaccion {transaction}...");

        ExecuteSapAction(session, "Maximizar ventana", () =>
        {
            FindById<GuiFrameWindow>(session, "wnd[0]")?.Maximize();
        });

        StartTransaction(session, transaction);
        WaitUntilSessionReady(session, 10, $"Transaccion {transaction}");

        ExecuteSapAction(session, "Ingresar centro", () =>
        {
            if (!TrySetText(session, "wnd[0]/usr/ctxtS_WERKS-LOW", plant))
            {
                TrySetText(session, "wnd[0]/usr/txtS_WERKS-LOW", plant);
            }
        });

        ExecuteSapAction(session, "Ejecutar consulta", () =>
        {
            FindById<GuiButton>(session, "wnd[0]/tbar[1]/btn[8]")?.Press();
        });
        WaitUntilSessionReady(session, 15, "Resultado COGI");

        ExecuteSapAction(session, "Abrir dialogo de exportacion", () =>
        {
            FindById<GuiButton>(session, "wnd[0]/tbar[1]/btn[20]")?.Press();
        });
        WaitUntilSessionReady(session, 5, "Dialogo exportacion");

        ExecuteSapAction(session, "Confirmar formato de exportacion", () =>
        {
            FindById<GuiButton>(session, "wnd[1]/tbar[0]/btn[0]")?.Press();
        });
        WaitUntilSessionReady(session, 5, "Confirmar exportacion");

        ExecuteSapAction(session, "Especificar nombre de archivo", () =>
        {
            TrySetText(session, "wnd[1]/usr/ctxtDY_FILENAME", exportFile);
        });

        ExecuteSapAction(session, "Guardar archivo", () =>
        {
            FindById<GuiButton>(session, "wnd[1]/tbar[0]/btn[11]")?.Press();
        });
        WaitUntilSessionReady(session, 15, "Guardar archivo");

        Console.WriteLine($"{Timestamp()} Script COGI ejecutado exitosamente.");
        Console.WriteLine($"{Timestamp()} El archivo '{exportFile}' deberia haberse generado en la ubicacion configurada en SAP.");
    }

    [SupportedOSPlatform("windows")]
    private static void StartTransaction(GuiSession session, string transaction)
    {
        ExecuteSapAction(session, $"Ir a transaccion {transaction}", () =>
        {
            try
            {
                session.StartTransaction(transaction);
            }
            catch
            {
                var okcd = FindById<GuiTextField>(session, "wnd[0]/tbar[0]/okcd");
                var wnd0 = FindById<GuiFrameWindow>(session, "wnd[0]");
                if (okcd == null || wnd0 == null)
                {
                    throw new InvalidOperationException("No se pudo navegar a la transaccion solicitada.");
                }

                okcd.Text = transaction;
                wnd0.SendVKey(0);
            }
        });
    }

    [SupportedOSPlatform("windows")]
    private static void ExecuteSapAction(GuiSession session, string description, Action action)
    {
        try
        {
            Console.WriteLine($"{Timestamp()} -> {description}...");
            action();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{Timestamp()} Error al ejecutar '{description}': {ex.Message}");
            Console.ResetColor();
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void WaitUntilSessionReady(GuiSession session, int timeoutSeconds, string context)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            bool busy = false;
            try
            {
                busy = session.Busy;
            }
            catch
            {
                // Ignorar y continuar intentando
            }

            if (!busy)
            {
                Thread.Sleep(300);
                try
                {
                    busy = session.Busy;
                }
                catch
                {
                    busy = false;
                }
            }

            if (!busy)
            {
                return;
            }

            Thread.Sleep(300);
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"{Timestamp()} Advertencia: Timeout esperando '{context}'.");
        Console.ResetColor();
    }

    [SupportedOSPlatform("windows")]
    private static bool TrySetText(GuiSession session, string id, string value)
    {
        try
        {
            var obj = session.FindById(id, false);
            if (obj == null)
            {
                return false;
            }

            switch (obj)
            {
                case GuiTextField textField:
                    textField.Text = value;
                    return true;
                case GuiCTextField cTextField:
                    cTextField.Text = value;
                    return true;
                default:
                    try
                    {
                        dynamic dynamicObj = obj;
                        dynamicObj.Text = value;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
            }
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static T? FindById<T>(GuiSession session, string id) where T : class
    {
        try
        {
            return session.FindById(id, false) as T;
        }
        catch
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static dynamic GetObject(string progId)
    {
        // Obtener la instancia activa del objeto COM
        // No es necesario verificar Type.GetTypeFromProgID ya que GetActiveObject
        // maneja la verificacion y error apropiadamente
        object obj = NativeMethods.GetActiveObject(progId);
        return obj;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is null) return;
        try
        {
            if (Marshal.IsComObject(value))
                Marshal.ReleaseComObject(value);
        }
        catch
        {
            // best-effort
        }
    }

    private static T? TryGet<T>(Func<T> getter)
    {
        try { return getter(); }
        catch { return default; }
    }

    private static string Timestamp() => $"[{DateTime.Now:HH:mm:ss}]";
}
