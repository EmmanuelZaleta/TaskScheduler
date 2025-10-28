# Implementación del Conector SAP GUI

## Descripción General

Esta implementación proporciona una interfaz fuertemente tipada para interactuar con SAP GUI Scripting Engine utilizando las librerías de interoperabilidad COM de SAP.

## Arquitectura

### Componentes Principales

1. **ISapGuiConnector** (`src/Sap/Contracts/ISapGuiConnector.cs`)
   - Interfaz que define el contrato para la conexión con SAP GUI
   - Proporciona métodos para conectar, autenticar y gestionar sesiones

2. **SapGuiConnector** (`src/Sap/Services/SapGuiConnector.cs`)
   - Implementación concreta de `ISapGuiConnector`
   - Maneja la conexión COM con SAP GUI
   - Gestiona el ciclo de vida de objetos COM
   - Registrado como Singleton en el contenedor de DI

3. **SapGuiConnectorUsageExample** (`src/Sap/Examples/SapGuiConnectorUsageExample.cs`)
   - Ejemplos de uso del conector
   - Casos de uso comunes

## Requisitos Previos

### Software Necesario

1. **SAP GUI for Windows** instalado y configurado
2. **SAP GUI Scripting habilitado**
   - Verificar en: SAP GUI → Options → Accessibility & Scripting → Scripting
   - Debe estar marcado "Enable scripting"

### DLLs de Interoperabilidad

Antes de compilar, debe copiar las siguientes DLLs al directorio `src/Sap/lib/`:

1. `Interop.SAPFEWSELib.dll` - Interoperabilidad con SAP GUI Scripting Engine
2. `Interop.SapROTWr.dll` - Interoperabilidad con SAP Running Object Table

#### Ubicación de las DLLs

Las DLLs se encuentran en:
```
C:\Users\90022817\source\repos\YCC.SapAutomation.Host\src\ExternalApps\YCC.AnalisisCogisInventario\obj\Debug\net8.0-windows10.0.17763.0\
```

#### Script de Instalación (PowerShell)

```powershell
# Ejecutar desde la raíz del repositorio
$sourceDir = "C:\Users\90022817\source\repos\YCC.SapAutomation.Host\src\ExternalApps\YCC.AnalisisCogisInventario\obj\Debug\net8.0-windows10.0.17763.0"
$destDir = "src\Sap\lib"

# Crear directorio si no existe
New-Item -ItemType Directory -Force -Path $destDir

# Copiar DLLs
Copy-Item "$sourceDir\Interop.SAPFEWSELib.dll" -Destination $destDir -Force
Copy-Item "$sourceDir\Interop.SapROTWr.dll" -Destination $destDir -Force

Write-Host "DLLs copiadas exitosamente" -ForegroundColor Green
Get-ChildItem $destDir\*.dll | Format-Table Name, Length, LastWriteTime
```

## Configuración

### appsettings.json

```json
{
  "Sap": {
    "Mode": "Gui",
    "Gui": {
      "BootstrapEnabled": true,
      "BootstrapOperationCode": "SAP_BOOTSTRAP",
      "ProcessName": "saplogon.exe",
      "MonitorIntervalSeconds": 60,
      "SystemId": ".YNCA - EQ2 - ERP QA2",
      "Client": "800",
      "User": "usuario",
      "Password": "contraseña",
      "Lang": "ES"
    }
  }
}
```

## Uso

### Registro en Dependency Injection

El servicio se registra automáticamente al llamar `AddSapAdapters`:

```csharp
services.AddSapAdapters(configuration);
```

### Ejemplo Básico

```csharp
public class MiServicio
{
    private readonly ISapGuiConnector _sapConnector;

    public MiServicio(ISapGuiConnector sapConnector)
    {
        _sapConnector = sapConnector;
    }

    public async Task EjecutarOperacion()
    {
        // 1. Conectar con SAP GUI
        await _sapConnector.ConnectAsync(startIfNotRunning: true, timeoutSeconds: 45);

        // 2. Obtener conexión al sistema
        var connection = await _sapConnector.GetOrCreateConnectionAsync(".YNCA - EQ2 - ERP QA2");

        // 3. Obtener sesión
        var session = _sapConnector.GetSession(connection);

        // 4. Login si es necesario
        if (!_sapConnector.IsAuthenticated(session))
        {
            await _sapConnector.LoginAsync(session, "800", "usuario", "password", "ES");
        }

        // 5. Ejecutar transacción
        session.StartTransaction("SE38");
    }
}
```

## API Principal

### ISapGuiConnector

#### Propiedades

- `GuiApplication Application` - Obtiene la aplicación SAP GUI activa
- `bool IsConnected` - Indica si hay conexión activa

#### Métodos

**ConnectAsync**
```csharp
Task<bool> ConnectAsync(bool startIfNotRunning = true, int timeoutSeconds = 45)
```
Conecta con SAP GUI Scripting Engine. Si `startIfNotRunning` es true, inicia SAP GUI automáticamente.

**GetOrCreateConnectionAsync**
```csharp
Task<GuiConnection> GetOrCreateConnectionAsync(string systemDescription, bool reuseExisting = true)
```
Obtiene una conexión existente o crea una nueva al sistema especificado.

**GetSession**
```csharp
GuiSession GetSession(GuiConnection connection, int sessionIndex = 0)
```
Obtiene la sesión de una conexión. Por defecto, obtiene la primera sesión (índice 0).

**LoginAsync**
```csharp
Task<bool> LoginAsync(GuiSession session, string? client, string user, string password, string? language = null)
```
Realiza el login en SAP con las credenciales proporcionadas.

**IsAuthenticated**
```csharp
bool IsAuthenticated(GuiSession session)
```
Verifica si una sesión está autenticada.

**Disconnect**
```csharp
void Disconnect()
```
Desconecta de SAP GUI y libera los objetos COM.

## Características

### Gestión Automática de Conexiones

- Reutilización de conexiones existentes
- Creación automática de conexiones si no existen
- Manejo de timeouts configurables

### Gestión de Sesiones

- Acceso a múltiples sesiones
- Verificación de autenticación
- Login automático

### Manejo de Errores

- Logging detallado de operaciones
- Manejo de excepciones COM
- Reintentos automáticos en operaciones críticas

### Gestión de Recursos

- Liberación automática de objetos COM
- Implementa IDisposable
- Registrado como Singleton para mejor rendimiento

## Limitaciones

1. **Solo Windows**: La implementación solo funciona en sistemas Windows con SAP GUI instalado
2. **Single-threaded**: SAP GUI COM no es thread-safe, usar con cuidado en escenarios multi-hilo
3. **Dependencia de SAP GUI**: Requiere SAP GUI instalado y en ejecución

## Solución de Problemas

### Error: "No se encontró la librería SapROTWr"

**Causa**: Las DLLs de interoperabilidad no están en el directorio correcto

**Solución**:
1. Verificar que las DLLs están en `src/Sap/lib/`
2. Ejecutar el script de instalación de DLLs

### Error: "SAP GUI no se registró en el tiempo límite configurado"

**Causa**: SAP GUI no está iniciando o no tiene scripting habilitado

**Solución**:
1. Verificar que SAP GUI está instalado
2. Habilitar scripting en SAP GUI Options
3. Aumentar el timeout en la llamada a `ConnectAsync`

### Error: "Failed to get SAP GUI scripting engine"

**Causa**: Problemas de permisos o configuración de seguridad

**Solución**:
1. Ejecutar como administrador
2. Verificar configuración de seguridad de COM en Windows
3. Verificar que SAP GUI Scripting está habilitado

## Referencias

- [SAP GUI Scripting API Documentation](https://help.sap.com/docs/sap_gui_for_windows/b47d018c3b9b45e897faf66a6c0885a8/babdf65f4d0a4bd8b40f5ff132cb12fa.html)
- [SAP GUI Scripting Security](https://launchpad.support.sap.com/#/notes/539144)

## Changelog

### v1.0.0 - 2025-10-28

- Implementación inicial de ISapGuiConnector
- Implementación de SapGuiConnector usando DLLs de interoperabilidad COM
- Registro en Dependency Injection
- Ejemplos de uso
- Documentación completa
