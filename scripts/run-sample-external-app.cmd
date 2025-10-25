@echo off
setlocal enabledelayedexpansion

REM Optional override
if not defined SAMPLE_EXT_APP_EXE (
  set "SAMPLE_EXT_APP_EXE=%~dp0..\src\ExternalApps\SampleExternalApp\bin\Release\net8.0\win-x64\publish\YCC.SampleExternalApp.exe"
)
if not exist "%SAMPLE_EXT_APP_EXE%" (
  set "SAMPLE_EXT_APP_EXE=%~dp0..\src\ExternalApps\SampleExternalApp\bin\Debug\net8.0\YCC.SampleExternalApp.exe"
)
if not exist "%SAMPLE_EXT_APP_EXE%" (
  echo [run-sample-external-app] No se encontro el EXE. Publica o compila el proyecto SampleExternalApp.
  exit /b 2
)

set "ARGS=%*"
if "%ARGS%"=="" (
  set "ARGS=--sleep 2 --hold 10 --title \"SampleExternalApp demo\" --write ..\logs\sample-external-output.log"
)

echo [run-sample-external-app] Ejecutando "%SAMPLE_EXT_APP_EXE%" %ARGS%
"%SAMPLE_EXT_APP_EXE%" %ARGS%
exit /b %ERRORLEVEL%
