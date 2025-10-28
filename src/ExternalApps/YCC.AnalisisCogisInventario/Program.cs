using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
            // Conectar a SAP GUI existente usando SAPFEWSELib
            var sapGuiAuto = GetObject("SAPGUI") as GuiApplication;
            if (sapGuiAuto == null)
            {
                throw new InvalidOperationException("No se pudo conectar a SAP GUI. Asegurese de que SAP GUI este abierto.");
            }

            application = sapGuiAuto.GetScriptingEngine as GuiApplication;
            if (application == null)
            {
                throw new InvalidOperationException("No se pudo obtener el motor de scripting de SAP GUI.");
            }

            Console.WriteLine($"{Timestamp()} SAP GUI encontrado.");

            // Verificar si hay conexiones
            if (application.Children.Count == 0)
            {
                throw new InvalidOperationException("No hay conexiones SAP abiertas. Por favor, inicie sesion en SAP primero.");
            }

            // Usar la primera conexion
            connection = application.Children.ElementAt(0) as GuiConnection;
            if (connection == null)
            {
                throw new InvalidOperationException("No se pudo obtener la conexion SAP.");
            }
            Console.WriteLine($"{Timestamp()} Conexion encontrada: {TryGet(() => connection.Description) ?? "N/A"}");

            // Verificar si hay sesiones
            if (connection.Children.Count == 0)
            {
                throw new InvalidOperationException("No hay sesiones activas en la conexion SAP.");
            }

            // Usar la primera sesion
            session = connection.Children.ElementAt(0) as GuiSession;
            if (session == null)
            {
                throw new InvalidOperationException("No se pudo obtener la sesion SAP.");
            }
            var currentUser = TryGet(() => session.Info.User) ?? "desconocido";
            Console.WriteLine($"{Timestamp()} Sesion encontrada. Usuario: {currentUser}");

            // Crear una nueva ventana/sesion si es necesario
            Console.WriteLine($"{Timestamp()} Creando nueva ventana SAP...");
            try
            {
                // Intentar crear una nueva sesion
                int sessionCount = connection.Children.Count;
                session.CreateSession();
                Thread.Sleep(2000); // Esperar a que la nueva ventana se abra

                // Obtener la nueva sesion
                int newSessionCount = connection.Children.Count;
                if (newSessionCount > sessionCount)
                {
                    session = connection.Children.ElementAt(newSessionCount - 1) as GuiSession;
                    Console.WriteLine($"{Timestamp()} Nueva ventana creada exitosamente.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{Timestamp()} Advertencia: No se pudo crear nueva ventana. Usando ventana actual. Error: {ex.Message}");
            }

            if (session == null)
            {
                throw new InvalidOperationException("La sesion SAP no es valida.");
            }

            // Ejecutar el script COGI
            Console.WriteLine($"{Timestamp()} Ejecutando transaccion COGI...");

            // Maximizar ventana
            ExecuteSapCommand(() =>
            {
                var mainWindow = session.FindById("wnd[0]") as GuiFrameWindow;
                mainWindow?.Maximize();
            }, "Maximizar ventana");

            // Ingresar codigo de transaccion COGI
            ExecuteSapCommand(() =>
            {
                var okcdField = session.FindById("wnd[0]/tbar[0]/okcd") as GuiTextField;
                if (okcdField != null)
                    okcdField.Text = "COGI";
            }, "Ingresar codigo de transaccion COGI");

            // Presionar Enter
            ExecuteSapCommand(() =>
            {
                var mainWindow = session.FindById("wnd[0]") as GuiFrameWindow;
                mainWindow?.SendVKey(0);
            }, "Ejecutar transaccion");
            Thread.Sleep(1500);

            // Ingresar centro 1815
            ExecuteSapCommand(() =>
            {
                var werksField = session.FindById("wnd[0]/usr/ctxtS_WERKS-LOW") as GuiCTextField;
                if (werksField != null)
                    werksField.Text = "1815";
            }, "Ingresar centro 1815");

            // Presionar boton ejecutar (btn[8])
            ExecuteSapCommand(() =>
            {
                var executeButton = session.FindById("wnd[0]/tbar[1]/btn[8]") as GuiButton;
                executeButton?.Press();
            }, "Ejecutar consulta");
            Thread.Sleep(2000);

            // Presionar boton exportar (btn[20])
            ExecuteSapCommand(() =>
            {
                var exportButton = session.FindById("wnd[0]/tbar[1]/btn[20]") as GuiButton;
                exportButton?.Press();
            }, "Abrir dialogo de exportacion");
            Thread.Sleep(1000);

            // Confirmar exportacion (wnd[1]/tbar[0]/btn[0])
            ExecuteSapCommand(() =>
            {
                var confirmButton = session.FindById("wnd[1]/tbar[0]/btn[0]") as GuiButton;
                confirmButton?.Press();
            }, "Confirmar tipo de exportacion");
            Thread.Sleep(1000);

            // Ingresar nombre de archivo
            ExecuteSapCommand(() =>
            {
                var filenameField = session.FindById("wnd[1]/usr/ctxtDY_FILENAME") as GuiCTextField;
                if (filenameField != null)
                    filenameField.Text = "cogi.txt";
            }, "Especificar nombre de archivo: cogi.txt");

            // Guardar archivo (wnd[1]/tbar[0]/btn[11])
            ExecuteSapCommand(() =>
            {
                var saveButton = session.FindById("wnd[1]/tbar[0]/btn[11]") as GuiButton;
                saveButton?.Press();
            }, "Guardar archivo");
            Thread.Sleep(1500);

            Console.WriteLine($"{Timestamp()} Script COGI ejecutado exitosamente.");
            Console.WriteLine($"{Timestamp()} El archivo 'cogi.txt' deberia haberse generado en la ubicacion configurada en SAP.");
        }
        finally
        {
            ReleaseComObject(session);
            ReleaseComObject(connection);
            ReleaseComObject(application);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ExecuteSapCommand(Action command, string description)
    {
        try
        {
            Console.WriteLine($"{Timestamp()} -> {description}...");
            command();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{Timestamp()} Advertencia al ejecutar '{description}': {ex.Message}");
            Console.ResetColor();
            throw;
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
