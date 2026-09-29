@echo off
setlocal
cd /d "%~dp0"
title Local Skirmish Diagnostics
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0collect-diagnostics.ps1" %*
set "REPORT_RESULT=%ERRORLEVEL%"
echo.
if not "%REPORT_RESULT%"=="0" echo Could not collect the report. Read the message above.
pause
exit /b %REPORT_RESULT%
