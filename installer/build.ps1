# Script de construcción del instalador YCC Job Host
# Requiere WiX Toolset 3.11 o superior instalado
# https://wixtoolset.org/releases/

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [string]$OutputDir = ".\bin\x64\Release"
)

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host " YCC Job Host - Installer Builder" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que WiX esté instalado
$wixPath = "${env:WIX}bin"
if (-not (Test-Path $wixPath)) {
    Write-Host "ERROR: WiX Toolset no está instalado." -ForegroundColor Red
    Write-Host "Por favor descargue e instale WiX Toolset desde: https://wixtoolset.org/releases/" -ForegroundColor Yellow
    exit 1
}

Write-Host "WiX Toolset encontrado en: $wixPath" -ForegroundColor Green

# Paso 1: Compilar la aplicación JobHost
Write-Host ""
Write-Host "Paso 1: Compilando aplicación JobHost..." -ForegroundColor Yellow
$jobHostPath = "..\src\JobHost"
Push-Location $jobHostPath

try {
    # Limpiar compilaciones anteriores
    Write-Host "  - Limpiando compilaciones anteriores..." -ForegroundColor Gray
    dotnet clean -c $Configuration | Out-Null

    # Publicar aplicación con todas las dependencias
    Write-Host "  - Publicando aplicación (esto puede tomar unos minutos)..." -ForegroundColor Gray
    $publishResult = dotnet publish -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=false 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Falló la compilación de JobHost" -ForegroundColor Red
        Write-Host $publishResult -ForegroundColor Red
        Pop-Location
        exit 1
    }

    Write-Host "  - Compilación exitosa!" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Verificar que los archivos compilados existan
$publishPath = "$jobHostPath\bin\$Configuration\net8.0-windows\win-x64\publish"
if (-not (Test-Path "$publishPath\YCC.JobHost.exe")) {
    Write-Host "ERROR: No se encontró el ejecutable compilado en: $publishPath" -ForegroundColor Red
    exit 1
}

Write-Host "  - Archivos de publicación encontrados en: $publishPath" -ForegroundColor Green

# Paso 2: Crear icono predeterminado si no existe
$iconPath = ".\icon.ico"
if (-not (Test-Path $iconPath)) {
    Write-Host ""
    Write-Host "Paso 2: Creando icono predeterminado..." -ForegroundColor Yellow
    Write-Host "  - NOTA: Para un instalador profesional, reemplace icon.ico con su propio icono" -ForegroundColor Gray

    # Copiar un icono del sistema si existe, sino crear uno básico
    $systemIcon = "C:\Windows\System32\imageres.dll"
    if (Test-Path $systemIcon) {
        Write-Host "  - Usando icono temporal del sistema" -ForegroundColor Gray
        # Por ahora solo crear un archivo vacío, el usuario debe proporcionar el icono real
        "ICO placeholder - replace with real icon" | Out-File $iconPath -Encoding ASCII
    }
}

# Paso 3: Compilar el instalador WiX
Write-Host ""
Write-Host "Paso 3: Compilando instalador MSI..." -ForegroundColor Yellow

# Compilar archivo .wxs a .wixobj
Write-Host "  - Compilando definición del producto (candle)..." -ForegroundColor Gray
$candleCmd = "`"$wixPath\candle.exe`" Product.wxs -ext WixUIExtension -ext WixUtilExtension -arch x64 -o obj\Product.wixobj"
Invoke-Expression $candleCmd

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Falló la compilación con candle.exe" -ForegroundColor Red
    exit 1
}

# Enlazar .wixobj a .msi
Write-Host "  - Enlazando instalador (light)..." -ForegroundColor Gray
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$lightCmd = "`"$wixPath\light.exe`" obj\Product.wixobj -ext WixUIExtension -ext WixUtilExtension -o `"$OutputDir\YCC.JobHost.Installer.msi`" -sval"
Invoke-Expression $lightCmd

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Falló el enlazado con light.exe" -ForegroundColor Red
    exit 1
}

# Paso 4: Finalización
Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host " Instalador creado exitosamente!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "Ubicación del instalador:" -ForegroundColor Cyan
Write-Host "  $OutputDir\YCC.JobHost.Installer.msi" -ForegroundColor White
Write-Host ""
Write-Host "Características del instalador:" -ForegroundColor Cyan
Write-Host "  * Instalación en Program Files\YCC\JobHost" -ForegroundColor White
Write-Host "  * Accesos directos en Menú Inicio y Escritorio" -ForegroundColor White
Write-Host "  * Inicio automático con Windows (minimizado)" -ForegroundColor White
Write-Host "  * Ejecución persistente en segundo plano" -ForegroundColor White
Write-Host "  * Desinstalador incluido" -ForegroundColor White
Write-Host ""
Write-Host "Para probar el instalador:" -ForegroundColor Yellow
Write-Host "  msiexec /i `"$OutputDir\YCC.JobHost.Installer.msi`" /l*v install.log" -ForegroundColor Gray
Write-Host ""
