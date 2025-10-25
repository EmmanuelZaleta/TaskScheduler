@echo off
set LOGDIR=%~dp0..\logs
if not exist "%LOGDIR%" (
  mkdir "%LOGDIR%"
)
echo [%date% %time%] Sample automation executed>>"%LOGDIR%\sample-automation.log"
