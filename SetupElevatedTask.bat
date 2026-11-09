@echo off
rem One-time setup: register a scheduled task that runs MemoryOptimizer.exe
rem with highest privileges, so it can start without UAC / SmartScreen prompts.
net session >nul 2>&1
if not "%errorlevel%"=="0" (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\setup-task.ps1"
pause