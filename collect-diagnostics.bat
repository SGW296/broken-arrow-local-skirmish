@echo off
setlocal
cd /d "%~dp0"
title Local Skirmish Diagnostics
set "PSModulePath=%SystemRoot%\System32\WindowsPowerShell\v1.0\Modules"
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0collect-diagnostics.ps1" %*
set "REPORT_RESULT=%ERRORLEVEL%"
echo.
if not "%REPORT_RESULT%"=="0" echo Could not collect the report. Read the message above.
pause
exit /b %REPORT_RESULT%
