$ErrorActionPreference = 'Stop'

# Registers an on-demand scheduled task that starts MemoryOptimizer.exe with
# highest privileges. Launching through the task skips both UAC and SmartScreen.
# Run this script elevated (SetupElevatedTask.bat does that for you).

$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'MemoryOptimizer.exe'
if (-not (Test-Path $exe)) {
    Write-Host "MemoryOptimizer.exe not found in $root"
    exit 1
}

$taskName = 'MemoryOptimizer-Elevated'
$command = '"' + $exe + '"'

& schtasks.exe /create /tn $taskName /tr $command /sc once /st 00:00 /rl highest /f
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Failed to create the task (exit code $LASTEXITCODE). Run this script as administrator."
    exit 1
}

Write-Host ""
Write-Host "Task created: $taskName"
Write-Host "Start the app with LaunchElevated.bat - it will run as administrator without prompts."
