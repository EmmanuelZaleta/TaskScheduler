using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SAPFEWSELib;
using SapROTWr;
using YCC.SapAutomation.Sap.Contracts;
using YCC.SapAutomation.Sap.Options;

namespace YCC.SapAutomation.Sap.Services;

/// <summary>
/// Implementación del conector con SAP GUI Scripting Engine
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SapGuiConnector : ISapGuiConnector
{
    private readonly ILogger<SapGuiConnector> _logger;
    private readonly SapOptions _options;
    private GuiApplication? _application;
    private SapROTWrapper? _rotWrapper;
    private bool _disposed;

    public SapGuiConnector(
        ILogger<SapGuiConnector> logger,
        IOptions<SapOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public GuiApplication Application
    {
        get
        {
            if (_application is null)
                throw new InvalidOperationException("No hay conexión activa con SAP GUI. Llame a ConnectAsync primero.");
            return _application;
        }
    }

    public bool IsConnected => _application is not null;

    public async Task<bool> ConnectAsync(bool startIfNotRunning = true, int timeoutSeconds = 45)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SapGuiConnector));

        if (IsConnected)
        {
            _logger.LogInformation("Ya existe una conexión activa con SAP GUI");
            return true;
        }

        try
        {
            _logger.LogInformation("Conectando con SAP GUI Scripting Engine...");

            _rotWrapper = new SapROTWrapper();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
            var launched = false;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var rotEntry = _rotWrapper.GetROTEntry("SAPGUI");
                    if (rotEntry is not null)
                    {
                        _application = (GuiApplication)rotEntry.GetScriptingEngine();
                        _logger.LogInformation("Conectado exitosamente a SAP GUI");
                        return true;
                    }
                }
                catch (COMException ex)
                {
                    _logger.LogTrace("SAP GUI aún no está disponible: {Message}", ex.Message);
                }

                if (!launched && startIfNotRunning)
                {
                    LaunchSapGui();
                    launched = true;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            _logger.LogError("Timeout esperando que SAP GUI se registre en el ROT");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al conectar con SAP GUI");
            return false;
        }
    }

    public async Task<GuiConnection> GetOrCreateConnectionAsync(string systemDescription, bool reuseExisting = true)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SapGuiConnector));

        if (!IsConnected)
            throw new InvalidOperationException("No hay conexión activa con SAP GUI");

        if (string.IsNullOrWhiteSpace(systemDescription))
            throw new ArgumentException("La descripción del sistema no puede estar vacía", nameof(systemDescription));

        try
        {
            // Buscar conexión existente
            if (reuseExisting)
            {
                var existingConnection = FindExistingConnection(systemDescription);
                if (existingConnection is not null)
                {
                    _logger.LogInformation("Reutilizando conexión existente a {System}", systemDescription);
                    return existingConnection;
                }
            }

            // Crear nueva conexión
            _logger.LogInformation("Abriendo nueva conexión a {System}", systemDescription);
            var connection = Application.OpenConnection(systemDescription, Sync: true);

            // Esperar a que la conexión esté lista
            await Task.Delay(TimeSpan.FromSeconds(2));

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener/crear conexión a {System}", systemDescription);
            throw;
        }
    }

    public GuiSession GetSession(GuiConnection connection, int sessionIndex = 0)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SapGuiConnector));

        if (connection is null)
            throw new ArgumentNullException(nameof(connection));

        try
        {
            var sessionCount = connection.Children.Count;
            if (sessionCount == 0)
                throw new InvalidOperationException("La conexión no tiene sesiones activas");

            if (sessionIndex >= sessionCount)
                throw new ArgumentOutOfRangeException(nameof(sessionIndex),
                    $"Índice de sesión {sessionIndex} fuera de rango (0-{sessionCount - 1})");

            var session = (GuiSession)connection.Children.ElementAt(sessionIndex);
            _logger.LogInformation("Obtenida sesión {Index} de la conexión", sessionIndex);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener sesión {Index}", sessionIndex);
            throw;
        }
    }

    public async Task<bool> LoginAsync(GuiSession session, string? client, string user, string password, string? language = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SapGuiConnector));

        if (session is null)
            throw new ArgumentNullException(nameof(session));

        if (string.IsNullOrWhiteSpace(user))
            throw new ArgumentException("El usuario no puede estar vacío", nameof(user));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("La contraseña no puede estar vacía", nameof(password));

        try
        {
            // Verificar si ya está autenticado
            if (IsAuthenticated(session))
            {
                var currentUser = session.Info.User;
                if (currentUser.Equals(user, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("La sesión ya está autenticada como {User}", currentUser);
                    return true;
                }
            }

            _logger.LogInformation("Realizando login como {User}", user);

            // Llenar campos de login
            if (!string.IsNullOrWhiteSpace(client))
            {
                TrySetFieldText(session, "wnd[0]/usr/txtRSYST-MANDT", client);
            }

            TrySetFieldText(session, "wnd[0]/usr/txtRSYST-BNAME", user);
            TrySetFieldText(session, "wnd[0]/usr/pwdRSYST-BCODE", password);

            if (!string.IsNullOrWhiteSpace(language))
            {
                TrySetFieldText(session, "wnd[0]/usr/txtRSYST-LANGU", language);
            }

            // Enviar Enter
            var mainWindow = (GuiFrameWindow)session.FindById("wnd[0]");
            mainWindow.SendVKey(0);

            // Esperar y verificar autenticación
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));

                // Manejar posibles popups
                HandlePopups(session);

                if (IsAuthenticated(session))
                {
                    var authenticatedUser = session.Info.User;
                    if (authenticatedUser.Equals(user, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Login exitoso como {User}", authenticatedUser);
                        return true;
                    }
                }
            }

            _logger.LogWarning("No se pudo confirmar la autenticación dentro del tiempo límite");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el login");
            return false;
        }
    }

    public bool IsAuthenticated(GuiSession session)
    {
        if (session is null)
            return false;

        try
        {
            var user = session.Info.User;
            return !string.IsNullOrWhiteSpace(user);
        }
        catch
        {
            return false;
        }
    }

    public void Disconnect()
    {
        if (_disposed)
            return;

        try
        {
            if (_application is not null)
            {
                _logger.LogInformation("Desconectando de SAP GUI");

                if (Marshal.IsComObject(_application))
                    Marshal.ReleaseComObject(_application);

                _application = null;
            }

            if (_rotWrapper is not null)
            {
                if (Marshal.IsComObject(_rotWrapper))
                    Marshal.ReleaseComObject(_rotWrapper);

                _rotWrapper = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al desconectar de SAP GUI");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Disconnect();
        _disposed = true;
    }

    private void LaunchSapGui()
    {
        try
        {
            var exePath = !string.IsNullOrWhiteSpace(_options.Gui.ProcessName)
                ? _options.Gui.ProcessName
                : "saplogon.exe";

            _logger.LogInformation("Iniciando SAP GUI: {ExePath}", exePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            using var process = Process.Start(startInfo);
            _logger.LogInformation("SAP GUI iniciado con PID {ProcessId}", process?.Id ?? -1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar SAP GUI");
            throw new InvalidOperationException($"No se pudo iniciar SAP GUI: {ex.Message}", ex);
        }
    }

    private GuiConnection? FindExistingConnection(string systemDescription)
    {
        try
        {
            var count = Application.Children.Count;
            for (int i = 0; i < count; i++)
            {
                var connection = (GuiConnection)Application.Children.ElementAt(i);
                var description = connection.Description ?? string.Empty;
                var name = connection.Name ?? string.Empty;

                if (MatchesSystem(description, systemDescription) ||
                    MatchesSystem(name, systemDescription))
                {
                    return connection;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error al buscar conexión existente");
        }

        return null;
    }

    private static bool MatchesSystem(string actual, string expected)
    {
        return !string.IsNullOrWhiteSpace(actual) &&
               actual.Trim().Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void TrySetFieldText(GuiSession session, string fieldId, string value)
    {
        try
        {
            var field = (GuiTextField)session.FindById(fieldId);
            field.Text = value;

            var isMasked = fieldId.Contains("pwd", StringComparison.OrdinalIgnoreCase);
            _logger.LogDebug("Campo {FieldId} establecido a {Value}", fieldId, isMasked ? "****" : value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo establecer el campo {FieldId}", fieldId);
        }
    }

    private void HandlePopups(GuiSession session)
    {
        try
        {
            var popup = (GuiFrameWindow)session.FindById("wnd[1]");
            if (popup is not null)
            {
                var text = popup.Text ?? string.Empty;
                _logger.LogInformation("Popup detectado: {Text}", text);

                // Enviar Enter para cerrar popup
                popup.SendVKey(0);
            }
        }
        catch
        {
            // No hay popup
        }
    }
}
