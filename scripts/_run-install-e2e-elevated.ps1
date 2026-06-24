param([string]$Version = "50.0.0")

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null
$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot
$log = Join-Path $ProjectRoot "artifacts\install-e2e-last.log"
Start-Transcript -Path $log -Force | Out-Null

try {
    & (Join-Path $PSScriptRoot "run-install-lifecycle-e2e.ps1") -Version $Version
    exit $LASTEXITCODE
}
catch {
    Write-Host "[FAIL] $_" -ForegroundColor Red
    exit 1
}
finally {
    Stop-Transcript | Out-Null
}