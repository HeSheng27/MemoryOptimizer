# Starts MemoryOptimizer.exe through CreateProcess, bypassing Explorer's SmartScreen check.
# Only use this for the exe built from this folder.
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'MemoryOptimizer.exe'
if (-not (Test-Path $exe)) {
    Write-Host "MemoryOptimizer.exe not found in $root"
    exit 1
}

$info = New-Object System.Diagnostics.ProcessStartInfo
$info.FileName = $exe
$info.WorkingDirectory = $root
$info.UseShellExecute = $false
if ($args -and $args.Count -gt 0) {
    $info.Arguments = ($args -join ' ')
}
[System.Diagnostics.Process]::Start($info) | Out-Null
