@echo off
rem Start MemoryOptimizer with administrator rights through the scheduled task.
schtasks /run /tn "MemoryOptimizer-Elevated" >nul 2>&1
if errorlevel 1 (
    echo.
    echo Scheduled task not found. Run SetupElevatedTask.bat as administrator once.
    echo.
    pause
)