@echo off
setlocal
rem Turns off the SmartScreen app/file check system-wide so unsigned local
rem executables (like MemoryOptimizer.exe) start without the "Windows protected
rem your PC" prompt. Reversible: run RestoreSmartScreenCheck.bat.
net session >nul 2>&1
if not "%errorlevel%"=="0" (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo Disabling SmartScreen app/file check...
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\System" /v EnableSmartScreen /t REG_DWORD /d 0 /f >nul
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer" /v SmartScreenEnabled /t REG_SZ /d Off /f >nul
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer" /v SmartScreenEnabled /t REG_SZ /d Off /f >nul
gpupdate /force >nul 2>&1

echo.
choice /c YN /m "Restart Explorer now to apply immediately"
if errorlevel 2 goto done
taskkill /f /im explorer.exe >nul 2>&1
start "" explorer.exe
:done
echo.
echo Done. Double-click MemoryOptimizer.exe - it should start without the prompt.
echo To undo: run RestoreSmartScreenCheck.bat
pause