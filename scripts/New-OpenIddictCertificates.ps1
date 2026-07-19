param(
    [Parameter(Mandatory = $true)]
    [string]$Password,

    [string]$OutputDirectory = ".\.local-certs"
)

$ErrorActionPreference = "Stop"

$resolvedOutput = Resolve-Path -LiteralPath (New-Item -ItemType Directory -Force -Path $OutputDirectory)
$securePassword = ConvertTo-SecureString $Password -AsPlainText -Force

function New-ExportedCertificate {
    param(
        [string]$Name,
        [string]$Subject,
        [string]$KeySpec
    )

    $certificate = New-SelfSignedCertificate `
        -Subject $Subject `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyExportPolicy Exportable `
        -KeySpec $KeySpec

    $pfxPath = Join-Path $resolvedOutput $Name
    Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $securePassword | Out-Null

    return [Convert]::ToBase64String([IO.File]::ReadAllBytes($pfxPath))
}

$signingBase64 = New-ExportedCertificate `
    -Name "lobilend-openiddict-signing.pfx" `
    -Subject "CN=LobiLend OpenIddict Signing" `
    -KeySpec "Signature"

$encryptionBase64 = New-ExportedCertificate `
    -Name "lobilend-openiddict-encryption.pfx" `
    -Subject "CN=LobiLend OpenIddict Encryption" `
    -KeySpec "KeyExchange"

Write-Host ""
Write-Host "Add these values to the API host environment:" -ForegroundColor Cyan
Write-Host ""
Write-Host "Auth__OpenIddict__SigningCertificateBase64=$signingBase64"
Write-Host "Auth__OpenIddict__SigningCertificatePassword=$Password"
Write-Host "Auth__OpenIddict__EncryptionCertificateBase64=$encryptionBase64"
Write-Host "Auth__OpenIddict__EncryptionCertificatePassword=$Password"
Write-Host ""
Write-Host "PFX files were written to $resolvedOutput. They are local secrets." -ForegroundColor Yellow
