@echo off
setlocal enabledelayedexpansion
REM Ejecuta el Automation CLI con un manifiesto de ejemplo.

if not defined AUTOMATION_CLI_EXE (
  set "AUTOMATION_CLI_EXE=%~dp0..\src\AutomationCli\bin\Release\net8.0\win-x64\publish\YCC.SapAutomation.AutomationCli.exe"
)

set "MANIFEST=%~1"
if not defined MANIFEST (
  set "MANIFEST=%~dp0..\automations\sample-manifest.json"
) else (
  shift
)

set "PIPELINE_ARGS=%PIPELINE_ARGS%"

if not exist "%AUTOMATION_CLI_EXE%" (
  echo [run-sample-automation] No se encontro el ejecutable del Automation CLI.
  echo [run-sample-automation] Publica el proyecto con: dotnet publish src\AutomationCli -c Release -r win-x64 --self-contained false
  exit /b 1
)

echo [run-sample-automation] Ejecutando "%AUTOMATION_CLI_EXE%" --manifest "%MANIFEST%" %PIPELINE_ARGS% %*
"%AUTOMATION_CLI_EXE%" --manifest "%MANIFEST%" %PIPELINE_ARGS% %*
set "RC=%ERRORLEVEL%"

if "%RC%"=="0" (
  echo [run-sample-automation] Ejecucion completada correctamente.
) else (
  echo [run-sample-automation] Ejecucion termino con codigo %RC%.
)

exit /b %RC%
