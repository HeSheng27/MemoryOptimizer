# Build script for Memory Optimizer (ASCII only, avoids PS 5.1 encoding issues)
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root 'src'
$outPath = Join-Path $root 'MemoryOptimizer.exe'
$iconPath = Join-Path $root 'app.ico'

$candidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $null
foreach ($candidate in $candidates) {
    if (Test-Path $candidate) { $csc = $candidate; break }
}
if (-not $csc) { throw 'csc.exe not found. .NET Framework 4.5+ is required.' }

$frameworkDir = Split-Path -Parent $csc
$sources = @(Get-ChildItem (Join-Path $srcDir '*.cs') | Sort-Object Name | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw 'No .cs files found in src.' }

$compilerArgs = @(
    '/nologo',
    '/target:winexe',
    '/optimize+',
    '/warn:4',
    '/codepage:65001',
    '/platform:anycpu',
    "/out:$outPath"
)
if (Test-Path $iconPath) { $compilerArgs += "/win32icon:$iconPath" }

# Embed every language file as MemoryOptimizer.lang.<file>
$langDir = Join-Path $root 'lang'
if (Test-Path $langDir) {
    Get-ChildItem $langDir -Filter *.ini | Sort-Object Name | ForEach-Object {
        $compilerArgs += "/resource:$($_.FullName),MemoryOptimizer.lang.$($_.Name)"
    }
}

$references = @('System.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll')
foreach ($reference in $references) {
    $compilerArgs += ('/r:' + (Join-Path $frameworkDir $reference))
}
$compilerArgs += $sources

& $csc $compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }

$file = Get-Item $outPath
Write-Host ('Build OK: {0} ({1:N1} KB)' -f $file.FullName, ($file.Length / 1KB))
