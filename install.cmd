@echo off
setlocal
set "PSARGS=%*"
set "PSARGS=%PSARGS:/nopause=%"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install.ps1" %PSARGS%
set "EXITCODE=%ERRORLEVEL%"

echo.
if not "%EXITCODE%"=="0" (
  echo PopMark setup failed with exit code %EXITCODE%.
)

echo %* | findstr /I /C:"/nopause" >nul
if errorlevel 1 pause

exit /b %EXITCODE%
