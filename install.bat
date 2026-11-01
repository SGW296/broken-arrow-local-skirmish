@echo off
setlocal
cd /d "%~dp0"
rem Use Windows PowerShell's own modules even when launched from PowerShell 7.
set "PSModulePath=%SystemRoot%\System32\WindowsPowerShell\v1.0\Modules"
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
set "BA_INSTALL_RESULT=%ERRORLEVEL%"
if not "%BA_INSTALL_RESULT%"=="0" (
  echo.
  echo The installer stopped with an error. Read the message above, then press a key to close.
  pause
)
exit /b %BA_INSTALL_RESULT%
