param(
    [string]$ExePath,
    [string]$PfxPath,
    [string]$Password,
    [string]$Thumbprint,
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)

# Signs MemoryOptimizer.exe with a code-signing certificate.
#
# Examples:
#   .\sign.ps1 -PfxPath D:\cert\codesign.pfx -Password 123456
#   .\sign.ps1 -Thumbprint 0123456789ABCDEF...
#   .\sign.ps1 -PfxPath ... -Password ... -ExePath D:\other\app.exe
#
# SmartScreen only accepts signatures from CA-issued certificates (OV/EV).
# A self-signed certificate only changes the publisher label; SmartScreen still
# warns. Use an EV certificate for immediate SmartScreen trust.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $root 'MemoryOptimizer.exe' }
if (-not (Test-Path $ExePath)) { throw "Executable not found: $ExePath" }

if (-not $PfxPath -and -not $Thumbprint) {
    throw "Provide -PfxPath (with -Password) or -Thumbprint of a certificate in CurrentUser\My."
}

$cert = $null
if ($PfxPath) {
    if (-not (Test-Path $PfxPath)) { throw "PFX not found: $PfxPath" }
    $secure = ConvertTo-SecureString $Password -AsPlainText -Force
    Write-Host "Importing certificate into CurrentUser\My..."
    try {
        $cert = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $secure
    } catch {
        Write-Host "Import failed, trying an in-memory key..."
        $flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($PfxPath, $Password, $flags)
    }
} else {
    $clean = $Thumbprint -replace '\s', ''
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $clean } | Select-Object -First 1
    if (-not $cert) { throw "Certificate not found in CurrentUser\My: $Thumbprint" }
}

if (-not $cert.HasPrivateKey) { throw "The certificate has no private key installed for this user." }

Write-Host "Signing:     $ExePath"
Write-Host "Certificate: $($cert.Subject)"
Write-Host "Thumbprint:  $($cert.Thumbprint)"

$result = Set-AuthenticodeSignature -FilePath $ExePath -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $TimestampServer

if ($result.Status -ne 'Valid') {
    Write-Host ""
    Write-Host "Signature status: $($result.Status)"
    Write-Host $result.StatusMessage
    Write-Host ""
    Write-Host "UntrustedRoot is expected for a self-signed certificate - SmartScreen will still warn."
    exit 1
}

Write-Host ""
Write-Host "Signed OK."
Write-Host "Publisher: $($result.SignerCertificate.Subject)"
if ($result.TimeStamperCertificate) {
    Write-Host "Timestamp: $($result.TimeStamperCertificate.NotBefore)"
}
