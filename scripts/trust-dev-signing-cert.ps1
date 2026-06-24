param(
    [switch]$MachineTrusted
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent

function Test-IsAdmin {
    ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}
$pfxPath = Join-Path $ProjectRoot "artifacts\signing\SmartPerformanceDoctor-dev.pfx"
if (-not (Test-Path $pfxPath)) {
    throw "Dev signing PFX not found: $pfxPath"
}

$password = if ($env:SPD_SIGN_CERT_PASSWORD) { $env:SPD_SIGN_CERT_PASSWORD } else { "SmartPerformanceDoctor-Dev-2026" }
$secure = ConvertTo-SecureString $password -AsPlainText -Force

Write-Host "== Trust dev code-signing certificate ==" -ForegroundColor Cyan

$imported = Import-PfxCertificate -FilePath $pfxPath -Password $secure -CertStoreLocation Cert:\CurrentUser\My -Exportable
Write-Host "[OK] Imported to CurrentUser\My: $($imported.Thumbprint)" -ForegroundColor Green



if ($MachineTrusted) {
    $cerPath = Join-Path $env:TEMP "PCCare-dev-signing.cer"
    Export-Certificate -Cert $imported -FilePath $cerPath -Force | Out-Null
    if (Test-IsAdmin) {
        certutil -addstore -f Root $cerPath | Out-Null
        Write-Host "[OK] Dev signing cert added to LocalMachine\Root" -ForegroundColor Green
    } else {
        certutil -user -addstore -f Root $cerPath | Out-Null
        Write-Host "[OK] Dev signing cert added to CurrentUser\Root" -ForegroundColor Green
    }
    Remove-Item $cerPath -Force -ErrorAction SilentlyContinue
}

Write-Host "[OK] Dev signing trust configured." -ForegroundColor Green