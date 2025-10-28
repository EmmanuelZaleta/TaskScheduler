using SAPFEWSELib;

namespace YCC.SapAutomation.Sap.Contracts;

/// <summary>
/// Interfaz para la gestión de conexiones con SAP GUI Scripting Engine
/// </summary>
public interface ISapGuiConnector : IDisposable
{
    /// <summary>
    /// Obtiene la aplicación SAP GUI activa
    /// </summary>
    GuiApplication Application { get; }

    /// <summary>
    /// Indica si hay una conexión activa con SAP GUI
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Conecta con SAP GUI Scripting Engine
    /// </summary>
    /// <param name="startIfNotRunning">Si es true, inicia SAP GUI si no está corriendo</param>
    /// <param name="timeoutSeconds">Tiempo de espera en segundos</param>
    /// <returns>True si la conexión fue exitosa</returns>
    Task<bool> ConnectAsync(bool startIfNotRunning = true, int timeoutSeconds = 45);

    /// <summary>
    /// Obtiene o crea una conexión a un sistema SAP
    /// </summary>
    /// <param name="systemDescription">Descripción del sistema (ej: ".YNCA - EQ2 - ERP QA2")</param>
    /// <param name="reuseExisting">Si es true, reutiliza una conexión existente</param>
    /// <returns>La conexión al sistema SAP</returns>
    Task<GuiConnection> GetOrCreateConnectionAsync(string systemDescription, bool reuseExisting = true);

    /// <summary>
    /// Obtiene la sesión activa de una conexión
    /// </summary>
    /// <param name="connection">La conexión de la cual obtener la sesión</param>
    /// <param name="sessionIndex">Índice de la sesión (por defecto 0)</param>
    /// <returns>La sesión SAP</returns>
    GuiSession GetSession(GuiConnection connection, int sessionIndex = 0);

    /// <summary>
    /// Realiza login en SAP con las credenciales proporcionadas
    /// </summary>
    /// <param name="session">La sesión donde hacer login</param>
    /// <param name="client">Cliente SAP</param>
    /// <param name="user">Usuario</param>
    /// <param name="password">Contraseña</param>
    /// <param name="language">Idioma (ej: "ES", "EN")</param>
    /// <returns>True si el login fue exitoso</returns>
    Task<bool> LoginAsync(GuiSession session, string? client, string user, string password, string? language = null);

    /// <summary>
    /// Verifica si una sesión está autenticada
    /// </summary>
    /// <param name="session">La sesión a verificar</param>
    /// <returns>True si la sesión está autenticada</returns>
    bool IsAuthenticated(GuiSession session);

    /// <summary>
    /// Desconecta de SAP GUI
    /// </summary>
    void Disconnect();
}
