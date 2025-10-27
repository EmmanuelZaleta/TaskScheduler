@echo off
REM Script de construcción simplificado para el instalador YCC Job Host
REM Para opciones avanzadas, use: powershell -ExecutionPolicy Bypass -File build.ps1

echo =====================================
echo  YCC Job Host - Installer Builder
echo =====================================
echo.

REM Verificar que PowerShell esté disponible
where powershell >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: PowerShell no esta disponible
    exit /b 1
)

REM Ejecutar script de PowerShell
powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ERROR: Fallo la construccion del instalador
    exit /b 1
)

echo.
echo Presione cualquier tecla para salir...
pause >nul
