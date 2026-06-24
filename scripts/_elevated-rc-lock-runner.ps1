param(
    [string]$Version = "50.0.0"
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null
$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot
$log = Join-Path $ProjectRoot "artifacts\elevated-rc-lock.log"
Start-Transcript -Path $log -Force | Out-Null

try {
    & (Join-Path $PSScriptRoot "trust-dev-signing-cert.ps1") -MachineTrusted
    & (Join-Path $PSScriptRoot "sign-runtime-payload.ps1")
    & (Join-Path $PSScriptRoot "verify-runtime-signatures.ps1")
    & (Join-Path $PSScriptRoot "run-install-lifecycle-e2e.ps1") -Version $Version
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "[OK] Elevated RC lock steps completed." -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "[FAIL] $_" -ForegroundColor Red
    exit 1
}
finally {
    Stop-Transcript | Out-Null
}