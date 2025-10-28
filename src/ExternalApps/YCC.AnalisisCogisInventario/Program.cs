using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

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

        dynamic? sapGuiAuto = null;
        dynamic? application = null;
        dynamic? connection = null;
        dynamic? session = null;

        try
        {
            // Conectar a SAP GUI existente
            sapGuiAuto = GetObject("SAPGUI");
            if (sapGuiAuto == null)
            {
                throw new InvalidOperationException("No se pudo conectar a SAP GUI. Asegurese de que SAP GUI este abierto.");
            }

            application = sapGuiAuto.GetScriptingEngine();
            if (application == null)
            {
                throw new InvalidOperationException("No se pudo obtener el motor de scripting de SAP GUI.");
            }

            Console.WriteLine($"{Timestamp()} SAP GUI encontrado.");

            // Verificar si hay conexiones
            int connectionCount = Convert.ToInt32(application.Children.Count);
            if (connectionCount == 0)
            {
                throw new InvalidOperationException("No hay conexiones SAP abiertas. Por favor, inicie sesion en SAP primero.");
            }

            // Usar la primera conexion
            connection = application.Children(0);
            Console.WriteLine($"{Timestamp()} Conexion encontrada: {TryGet(() => (string?)connection.Description) ?? "N/A"}");

            // Verificar si hay sesiones
            int sessionCount = Convert.ToInt32(connection.Children.Count);
            if (sessionCount == 0)
            {
                throw new InvalidOperationException("No hay sesiones activas en la conexion SAP.");
            }

            // Usar la primera sesion
            session = connection.Children(0);
            var currentUser = TryGet(() => (string?)session.Info.User) ?? "desconocido";
            Console.WriteLine($"{Timestamp()} Sesion encontrada. Usuario: {currentUser}");

            // Crear una nueva ventana/sesion si es necesario
            Console.WriteLine($"{Timestamp()} Creando nueva ventana SAP...");
            try
            {
                // Intentar crear una nueva sesion
                session.CreateSession();
                Thread.Sleep(2000); // Esperar a que la nueva ventana se abra

                // Obtener la nueva sesion
                int newSessionCount = Convert.ToInt32(connection.Children.Count);
                if (newSessionCount > sessionCount)
                {
                    session = connection.Children(newSessionCount - 1);
                    Console.WriteLine($"{Timestamp()} Nueva ventana creada exitosamente.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{Timestamp()} Advertencia: No se pudo crear nueva ventana. Usando ventana actual. Error: {ex.Message}");
            }

            // Ejecutar el script COGI
            Console.WriteLine($"{Timestamp()} Ejecutando transaccion COGI...");

            // Maximizar ventana
            ExecuteSapCommand(() => session.findById("wnd[0]").maximize(), "Maximizar ventana");

            // Ingresar codigo de transaccion COGI
            ExecuteSapCommand(() =>
            {
                session.findById("wnd[0]/tbar[0]/okcd").text = "COGI";
            }, "Ingresar codigo de transaccion COGI");

            // Presionar Enter
            ExecuteSapCommand(() => session.findById("wnd[0]").sendVKey(0), "Ejecutar transaccion");
            Thread.Sleep(1500);

            // Ingresar centro 1815
            ExecuteSapCommand(() =>
            {
                session.findById("wnd[0]/usr/ctxtS_WERKS-LOW").text = "1815";
            }, "Ingresar centro 1815");

            // Presionar boton ejecutar (btn[8])
            ExecuteSapCommand(() => session.findById("wnd[0]/tbar[1]/btn[8]").press(), "Ejecutar consulta");
            Thread.Sleep(2000);

            // Presionar boton exportar (btn[20])
            ExecuteSapCommand(() => session.findById("wnd[0]/tbar[1]/btn[20]").press(), "Abrir dialogo de exportacion");
            Thread.Sleep(1000);

            // Confirmar exportacion (wnd[1]/tbar[0]/btn[0])
            ExecuteSapCommand(() => session.findById("wnd[1]/tbar[0]/btn[0]").press(), "Confirmar tipo de exportacion");
            Thread.Sleep(1000);

            // Ingresar nombre de archivo
            ExecuteSapCommand(() =>
            {
                session.findById("wnd[1]/usr/ctxtDY_FILENAME").text = "cogi.txt";
            }, "Especificar nombre de archivo: cogi.txt");

            // Guardar archivo (wnd[1]/tbar[0]/btn[11])
            ExecuteSapCommand(() => session.findById("wnd[1]/tbar[0]/btn[11]").press(), "Guardar archivo");
            Thread.Sleep(1500);

            Console.WriteLine($"{Timestamp()} Script COGI ejecutado exitosamente.");
            Console.WriteLine($"{Timestamp()} El archivo 'cogi.txt' deberia haberse generado en la ubicacion configurada en SAP.");
        }
        finally
        {
            ReleaseComObject(session);
            ReleaseComObject(connection);
            ReleaseComObject(application);
            ReleaseComObject(sapGuiAuto);
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
