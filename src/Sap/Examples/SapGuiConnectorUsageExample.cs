using Microsoft.Extensions.Logging;
using YCC.SapAutomation.Sap.Contracts;

namespace YCC.SapAutomation.Sap.Examples;

/// <summary>
/// Ejemplos de uso del SapGuiConnector
/// </summary>
public class SapGuiConnectorUsageExample
{
    private readonly ISapGuiConnector _sapConnector;
    private readonly ILogger<SapGuiConnectorUsageExample> _logger;

    public SapGuiConnectorUsageExample(
        ISapGuiConnector sapConnector,
        ILogger<SapGuiConnectorUsageExample> logger)
    {
        _sapConnector = sapConnector;
        _logger = logger;
    }

    /// <summary>
    /// Ejemplo básico: Conectar, hacer login y ejecutar una transacción
    /// </summary>
    public async Task BasicExample()
    {
        try
        {
            // 1. Conectar con SAP GUI
            _logger.LogInformation("Conectando con SAP GUI...");
            var connected = await _sapConnector.ConnectAsync(
                startIfNotRunning: true,
                timeoutSeconds: 45);

            if (!connected)
            {
                _logger.LogError("No se pudo conectar con SAP GUI");
                return;
            }

            // 2. Obtener o crear conexión al sistema
            _logger.LogInformation("Conectando al sistema SAP...");
            var connection = await _sapConnector.GetOrCreateConnectionAsync(
                systemDescription: ".YNCA - EQ2 - ERP QA2",
                reuseExisting: true);

            // 3. Obtener la sesión
            var session = _sapConnector.GetSession(connection, sessionIndex: 0);

            // 4. Hacer login si no está autenticado
            if (!_sapConnector.IsAuthenticated(session))
            {
                _logger.LogInformation("Realizando login...");
                var loginSuccess = await _sapConnector.LoginAsync(
                    session: session,
                    client: "800",
                    user: "usuario",
                    password: "contraseña",
                    language: "ES");

                if (!loginSuccess)
                {
                    _logger.LogError("Login fallido");
                    return;
                }
            }

            // 5. Ejecutar transacción
            _logger.LogInformation("Ejecutando transacción SE38...");
            session.StartTransaction("SE38");

            _logger.LogInformation("Operación completada exitosamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la operación");
        }
    }

    /// <summary>
    /// Ejemplo avanzado: Navegar por la interfaz de SAP
    /// </summary>
    public async Task AdvancedExample()
    {
        try
        {
            // Conectar y obtener sesión
            await _sapConnector.ConnectAsync();
            var connection = await _sapConnector.GetOrCreateConnectionAsync(".YNCA - EQ2 - ERP QA2");
            var session = _sapConnector.GetSession(connection);

            // Asegurar que estamos autenticados
            if (!_sapConnector.IsAuthenticated(session))
            {
                await _sapConnector.LoginAsync(session, "800", "usuario", "contraseña", "ES");
            }

            // Ir a la transacción SE16 (Data Browser)
            session.StartTransaction("SE16");

            // Esperar a que cargue la pantalla
            await Task.Delay(1000);

            // Ingresar nombre de tabla
            var tableField = session.FindById("wnd[0]/usr/ctxtDATABROWSE-TABLENAME");
            tableField.GetType().GetProperty("Text")?.SetValue(tableField, "MARA");

            // Presionar Enter
            var mainWindow = session.FindById("wnd[0]");
            mainWindow.GetType().GetMethod("SendVKey")?.Invoke(mainWindow, new object[] { 0 });

            // Esperar resultados
            await Task.Delay(2000);

            // Ejecutar (F8)
            mainWindow.GetType().GetMethod("SendVKey")?.Invoke(mainWindow, new object[] { 8 });

            _logger.LogInformation("Navegación completada");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en navegación avanzada");
        }
    }

    /// <summary>
    /// Ejemplo de manejo de múltiples sesiones
    /// </summary>
    public async Task MultiSessionExample()
    {
        try
        {
            await _sapConnector.ConnectAsync();
            var connection = await _sapConnector.GetOrCreateConnectionAsync(".YNCA - EQ2 - ERP QA2");

            // Obtener la primera sesión
            var session1 = _sapConnector.GetSession(connection, 0);

            if (!_sapConnector.IsAuthenticated(session1))
            {
                await _sapConnector.LoginAsync(session1, "800", "usuario", "contraseña", "ES");
            }

            // Crear nueva sesión
            session1.CreateSession();
            await Task.Delay(2000); // Esperar a que se cree la sesión

            // Obtener la segunda sesión
            var session2 = _sapConnector.GetSession(connection, 1);

            // Trabajar con ambas sesiones
            session1.StartTransaction("SE38");
            session2.StartTransaction("SE16");

            _logger.LogInformation("Trabajando con múltiples sesiones");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error manejando múltiples sesiones");
        }
    }

    /// <summary>
    /// Ejemplo de lectura de datos de la pantalla
    /// </summary>
    public async Task<string?> ReadFieldExample()
    {
        try
        {
            await _sapConnector.ConnectAsync();
            var connection = await _sapConnector.GetOrCreateConnectionAsync(".YNCA - EQ2 - ERP QA2");
            var session = _sapConnector.GetSession(connection);

            if (!_sapConnector.IsAuthenticated(session))
            {
                await _sapConnector.LoginAsync(session, "800", "usuario", "contraseña", "ES");
            }

            // Leer información del usuario actual
            var userInfo = session.Info;
            var currentUser = userInfo.User;
            var client = userInfo.Client;
            var systemName = userInfo.SystemName;

            _logger.LogInformation(
                "Usuario conectado: {User}, Cliente: {Client}, Sistema: {System}",
                currentUser, client, systemName);

            return currentUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo datos de la pantalla");
            return null;
        }
    }
}
