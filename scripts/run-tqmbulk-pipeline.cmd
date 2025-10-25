@echo off

REM -----------------------------------------------------------------------------
REM  Wrapper para ejecutar la pipeline TQMBULK usando el proyecto CLI interno.
REM  1) Publica el proyecto (ej. dotnet publish src/TqmbulkCli -c Release -r win-x64)
REM  2) Ajusta TQMBULK_CLI_EXE si necesitas ubicar el ejecutable en otra ruta.
REM  3) Opcionalmente pasa argumentos extra (ej. run-tqmbulk-pipeline.cmd --verbose)
REM -----------------------------------------------------------------------------

@setlocal enabledelayedexpansion

if not defined TQMBULK_CLI_EXE (
  REM Preferir publicado; si no existe, usar Debug.
  set "TQMBULK_CLI_EXE=%~dp0..\src\AutomationCli\bin\Release\net8.0\win-x64\publish\YCC.SapAutomation.AutomationCli.exe"
)
if not exist "%TQMBULK_CLI_EXE%" (
  set "TQMBULK_CLI_EXE=%~dp0..\src\AutomationCli\bin\Debug\net8.0\YCC.SapAutomation.AutomationCli.exe"
)
if not exist "%TQMBULK_CLI_EXE%" (
  echo [run-tqmbulk-pipeline] No se encontro un ejecutable del Automation CLI.
  echo Publica con: dotnet publish ..\src\AutomationCli -c Release -r win-x64 --self-contained false
  echo o compila en Debug y vuelve a intentar.
  exit /b 2
)

set "DEFAULT_MANIFEST=%~dp0..\automations\tqmbulk-pipeline.json"

if "%~1"=="" (
  set "PIPELINE_ARGS=--manifest \"%DEFAULT_MANIFEST%\""
) else (
  set "PIPELINE_ARGS=%*"
)

echo [run-tqmbulk-pipeline] Ejecutando "%TQMBULK_CLI_EXE%" %PIPELINE_ARGS%
"%TQMBULK_CLI_EXE%" %PIPELINE_ARGS%

set "RC=%ERRORLEVEL%"
if "%RC%"=="0" (
  echo [run-tqmbulk-pipeline] Ejecucion completada correctamente.
) else (
  echo [run-tqmbulk-pipeline] Ejecucion termino con codigo %RC%.
)

exit /b %RC%
