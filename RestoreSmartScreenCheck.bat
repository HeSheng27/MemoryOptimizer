@echo off
setlocal
rem Restores the default SmartScreen app/file check (enabled, Warn level).
net session >nul 2>&1
if not "%errorlevel%"=="0" (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo Restoring SmartScreen app/file check...
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\System" /v EnableSmartScreen /f >nul 2>&1
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer" /v SmartScreenEnabled /t REG_SZ /d Warn /f >nul
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer" /v SmartScreenEnabled /t REG_SZ /d Warn /f >nul
gpupdate /force >nul 2>&1

echo.
choice /c YN /m "Restart Explorer now to apply immediately"
if errorlevel 2 goto done
taskkill /f /im explorer.exe >nul 2>&1
start "" explorer.exe
:done
echo.
echo Restored. SmartScreen app/file check is back on.
pause