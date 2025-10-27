# YCC Job Host - Instalador Profesional

Este directorio contiene el proyecto de instalador profesional para **YCC Job Host - Task Scheduler** utilizando WiX Toolset.

## Características del Instalador

✅ **Instalación Profesional MSI**
- Instalador estándar de Windows (.msi)
- Compatible con políticas de grupo empresariales
- Soporte para instalación silenciosa
- Desinstalador completo integrado

✅ **Persistencia y Ejecución en Segundo Plano**
- La aplicación se ejecuta en la bandeja del sistema (system tray)
- Continúa ejecutándose aunque se cierre la ventana
- Se minimiza a la bandeja en lugar de cerrarse
- Menú contextual completo en el ícono de la bandeja

✅ **Inicio Automático con Windows**
- Se registra automáticamente en el inicio de Windows
- Inicia minimizado en segundo plano
- El usuario puede habilitar/deshabilitar desde la interfaz
- No requiere privilegios de administrador para el autostart

✅ **Accesos Directos**
- Acceso directo en el Menú Inicio
- Acceso directo opcional en el Escritorio
- Opción de desinstalación en el Menú Inicio

✅ **Estructura de Instalación Profesional**
```
C:\Program Files\YCC\JobHost\
├── YCC.JobHost.exe              (Ejecutable principal)
├── *.dll                         (Dependencias)
├── appsettings.json             (Configuración)
├── logs\                         (Carpeta de logs)
├── automations\                  (Definiciones de automatizaciones)
└── scripts\                      (Scripts auxiliares)
```

## Requisitos Previos

### Para Construir el Instalador:

1. **WiX Toolset 3.11 o superior**
   - Descargar desde: https://wixtoolset.org/releases/
   - Instalar la versión completa (no solo build tools)
   - Verificar que la variable de entorno `WIX` esté configurada

2. **.NET 8.0 SDK**
   - Para compilar la aplicación JobHost
   - Descargar desde: https://dotnet.microsoft.com/download

3. **Visual Studio 2022** (opcional)
   - Facilita la edición del proyecto WiX
   - Instalar extensión "WiX Toolset Visual Studio Extension"

### Para Instalar el Producto:

1. **.NET 8.0 Runtime** (o .NET Desktop Runtime)
   - Se instalará automáticamente si no está presente
   - Descargar desde: https://dotnet.microsoft.com/download/dotnet/8.0

## Construcción del Instalador

### Método 1: Script Automatizado (Recomendado)

```batch
# Abrir PowerShell o CMD en este directorio
build.bat
```

El script automáticamente:
1. Compila la aplicación JobHost en modo Release
2. Publica todos los archivos necesarios
3. Compila el instalador WiX
4. Genera el archivo MSI en `bin\x64\Release\`

### Método 2: PowerShell con Opciones

```powershell
# Compilación Release (predeterminado)
.\build.ps1

# Compilación Debug
.\build.ps1 -Configuration Debug

# Especificar directorio de salida
.\build.ps1 -OutputDir "C:\Installers"
```

### Método 3: MSBuild (Avanzado)

```batch
# Usando MSBuild directamente
msbuild YCC.JobHost.Installer.wixproj /p:Configuration=Release /p:Platform=x64
```

## Instalación del Producto

### Instalación Interactiva

Doble clic en el archivo `.msi` o:

```batch
YCC.JobHost.Installer.msi
```

### Instalación Silenciosa (Para Despliegues Empresariales)

```batch
# Instalación silenciosa con log
msiexec /i YCC.JobHost.Installer.msi /qn /l*v install.log

# Instalación silenciosa sin reinicio
msiexec /i YCC.JobHost.Installer.msi /qn /norestart

# Instalación con interfaz básica
msiexec /i YCC.JobHost.Installer.msi /qb
```

### Opciones de Instalación

| Parámetro | Descripción | Ejemplo |
|-----------|-------------|---------|
| `/i` | Instalar | `msiexec /i installer.msi` |
| `/x` | Desinstalar | `msiexec /x installer.msi` |
| `/qn` | Sin interfaz (silencioso) | `msiexec /i installer.msi /qn` |
| `/qb` | Interfaz básica | `msiexec /i installer.msi /qb` |
| `/l*v` | Log detallado | `msiexec /i installer.msi /l*v log.txt` |
| `INSTALLFOLDER` | Directorio personalizado | `INSTALLFOLDER="C:\CustomPath"` |

## Desinstalación

### Método 1: Panel de Control
1. Ir a Panel de Control → Programas → Programas y características
2. Buscar "YCC Job Host - Task Scheduler"
3. Hacer clic en Desinstalar

### Método 2: Menú Inicio
1. Ir a Menú Inicio → YCC Job Host
2. Hacer clic en "Desinstalar YCC Job Host"

### Método 3: Línea de Comandos
```batch
# Desinstalación silenciosa
msiexec /x {ProductCode} /qn
```

## Personalización del Instalador

### Cambiar Información del Producto

Editar `Product.wxs`, sección de variables:

```xml
<?define ProductName = "YCC Job Host - Task Scheduler" ?>
<?define ProductVersion = "1.0.0.0" ?>
<?define Manufacturer = "YCC Corporation" ?>
```

### Agregar Icono Personalizado

1. Crear o obtener un archivo `.ico` (debe contener múltiples resoluciones: 16x16, 32x32, 48x48, 256x256)
2. Guardar como `icon.ico` en este directorio
3. Reconstruir el instalador

### Agregar Banner e Imágenes

Descomentar y proporcionar en `Product.wxs`:

```xml
<WixVariable Id="WixUIBannerBmp" Value="Banner.bmp" />    <!-- 493x58 px -->
<WixVariable Id="WixUIDialogBmp" Value="Dialog.bmp" />   <!-- 493x312 px -->
```

### Modificar Licencia

Editar `License.rtf` con un editor RTF (WordPad, Word, etc.)

## Verificación del Instalador

### Verificar Firma Digital (Opcional)

```batch
signtool verify /pa YCC.JobHost.Installer.msi
```

### Firmar el Instalador (Para Producción)

```batch
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com YCC.JobHost.Installer.msi
```

## Troubleshooting

### Error: "WiX Toolset no está instalado"

**Solución:**
1. Descargar WiX Toolset desde https://wixtoolset.org/releases/
2. Instalar la versión 3.11 o superior
3. Reiniciar la terminal/PowerShell

### Error: "No se encontró el ejecutable compilado"

**Solución:**
1. Verificar que la aplicación JobHost compile correctamente
2. Ejecutar manualmente:
   ```batch
   cd ..\src\JobHost
   dotnet publish -c Release -r win-x64
   ```

### Error: "LGHT0001: El archivo fuente no existe"

**Solución:**
1. Verificar que todos los archivos referenciados en `Product.wxs` existan
2. Ajustar las rutas `Source` en el archivo WXS según sea necesario

### El instalador se crea pero falla al instalar

**Solución:**
1. Revisar el log de instalación:
   ```batch
   msiexec /i installer.msi /l*v install.log
   ```
2. Buscar errores en `install.log`

## Características de la Aplicación Instalada

Una vez instalada, la aplicación:

1. **Se ejecuta en segundo plano:**
   - Ícono visible en la bandeja del sistema (system tray)
   - No se cierra al hacer clic en X, se minimiza a la bandeja
   - Solo sale completamente mediante "Salir" del menú contextual

2. **Inicia con Windows automáticamente:**
   - Registrada en: `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
   - Inicia minimizada con parámetro `--minimized`
   - Se puede habilitar/deshabilitar desde la interfaz

3. **Menú contextual en bandeja:**
   - Abrir Dashboard
   - Pausar/Reanudar Scheduler
   - Salir

4. **Persistencia garantizada:**
   - Continúa ejecutando tareas programadas aunque se "cierre" la ventana
   - Logs detallados en `C:\Program Files\YCC\JobHost\logs\`

## Estructura del Proyecto

```
installer/
├── Product.wxs                 # Definición principal del instalador
├── YCC.JobHost.Installer.wixproj  # Proyecto WiX
├── License.rtf                 # Licencia EULA
├── icon.ico                    # Icono del producto (crear/reemplazar)
├── build.ps1                   # Script de construcción PowerShell
├── build.bat                   # Script de construcción Batch
├── README.md                   # Esta documentación
└── bin/                        # Salida del instalador (generado)
    └── x64/
        └── Release/
            └── YCC.JobHost.Installer.msi
```

## Soporte y Contacto

Para soporte técnico o consultas:
- Email: support@ycc.com
- Web: https://www.ycc.com/support

## Licencia

Copyright (c) 2024 YCC Corporation. Todos los derechos reservados.

---

**Última actualización:** Octubre 2024
**Versión del instalador:** 1.0.0
**Plataforma:** Windows 10/11 x64
