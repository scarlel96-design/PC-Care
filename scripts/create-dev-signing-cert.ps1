param(
    [string]$OutputPath = "",
    [string]$Password = "SmartPerformanceDoctor-Dev-2026"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

if (-not $OutputPath) {
    $OutputPath = Join-Path $ProjectRoot "artifacts\signing\SmartPerformanceDoctor-dev.pfx"
}

Write-Host "== Create dev code-signing certificate ==" -ForegroundColor Cyan

$dir = Split-Path $OutputPath -Parent
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$cert = New-SelfSignedCertificate `
    -Subject "CN=Smart Performance Doctor Dev" `
    -Type CodeSigningCert `
    -KeyUsage DigitalSignature `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(3)

$secure = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $OutputPath -Password $secure | Out-Null

Write-Host "[OK] Dev certificate: $OutputPath" -ForegroundColor Green
Write-Host "     Thumbprint: $($cert.Thumbprint)" -ForegroundColor Gray
Write-Host ""
Write-Host "사용 예:" -ForegroundColor Yellow
Write-Host '  $env:SPD_SIGN_CERT_PATH = ".\artifacts\signing\SmartPerformanceDoctor-dev.pfx"' -ForegroundColor Gray
Write-Host '  $env:SPD_SIGN_CERT_PASSWORD = "SmartPerformanceDoctor-Dev-2026"' -ForegroundColor Gray
Write-Host '  .\scripts\sign-consumer.ps1' -ForegroundColor Gray
Write-Host ""
Write-Host "참고: 자체 서명 인증서는 SmartScreen 경고를 제거하지 않습니다. 상용 배포에는 EV/OV 인증서가 필요합니다." -ForegroundColor DarkYellow