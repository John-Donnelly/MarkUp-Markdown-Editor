param(
    [Parameter(Mandatory = $true)]
    [string]$PfxPath,

    [Parameter(Mandatory = $true)]
    [string]$Password,

    [Parameter(Mandatory = $true)]
    [string]$CerPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PfxPath)) {
    throw "PFX file '$PfxPath' was not found."
}

$cerDirectory = Split-Path -Path $CerPath -Parent
if (-not (Test-Path -LiteralPath $cerDirectory)) {
    New-Item -Path $cerDirectory -ItemType Directory -Force | Out-Null
}

$certificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $PfxPath,
    $Password,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)

[System.IO.File]::WriteAllBytes(
    $CerPath,
    $certificate.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
