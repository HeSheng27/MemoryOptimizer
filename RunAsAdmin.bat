@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath (Join-Path (Get-Location) 'MemoryOptimizer.exe') -Verb RunAs"