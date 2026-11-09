@echo off
rem Launch without going through Explorer's SmartScreen check.
rem Use this only for the MemoryOptimizer.exe built from this folder.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\launch.ps1" %*