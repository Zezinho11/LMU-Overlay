param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,
    [Parameter(Mandatory = $true)]
    [string]$CertificatePath,
    [Parameter(Mandatory = $true)]
    [SecureString]$CertificatePassword,
    [string]$TimestampServer = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "Executable not found: $ExecutablePath"
}
if (-not (Test-Path -LiteralPath $CertificatePath -PathType Leaf)) {
    throw "Certificate not found: $CertificatePath"
}

$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $CertificatePath,
    $CertificatePassword,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
$signature = Set-AuthenticodeSignature `
    -LiteralPath $ExecutablePath `
    -Certificate $certificate `
    -TimestampServer $TimestampServer `
    -HashAlgorithm SHA256

if ($signature.Status -ne "Valid") {
    throw "Authenticode signing failed: $($signature.StatusMessage)"
}

Write-Host "Signed $ExecutablePath with $($certificate.Subject)."
