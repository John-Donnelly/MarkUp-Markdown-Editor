param(
    [Parameter(Mandatory = $true)]
    [string]$Publisher,

    [Parameter(Mandatory = $true)]
    [string]$PfxPath,

    [Parameter(Mandatory = $true)]
    [string]$Password
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (Test-Path -LiteralPath $PfxPath) {
    return
}

$pfxDirectory = Split-Path -Path $PfxPath -Parent
if (-not (Test-Path -LiteralPath $pfxDirectory)) {
    New-Item -Path $pfxDirectory -ItemType Directory -Force | Out-Null
}

$certificate = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Publisher `
    -KeyUsage DigitalSignature `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -TextExtension @(
        '2.5.29.37={text}1.3.6.1.5.5.7.3.3',
        '2.5.29.19={text}'
    ) `
    -CertStoreLocation 'Cert:\CurrentUser\My'

$securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
Export-PfxCertificate -Cert $certificate -FilePath $PfxPath -Password $securePassword | Out-Null
