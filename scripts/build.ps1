param(
    [switch]$SkipTests,
    [switch]$SkipInstaller
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null
$env:DOTNET_CLI_UI_LANGUAGE = "ko"
$env:PYTHONUTF8 = "1"

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

$Version = "50.0.0"
Write-Host "== PC 케어 프로 v$Version Build ==" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "check-environment.ps1")
& (Join-Path $PSScriptRoot "scan-private-key.ps1")
& (Join-Path $PSScriptRoot "clean-workspace.ps1") -KeepDist
& (Join-Path $PSScriptRoot "generate-commercial-packs.ps1") -Version $Version
& (Join-Path $PSScriptRoot "sign-commercial-packs.ps1") -Version $Version
& (Join-Path $PSScriptRoot "build-core.ps1")
& (Join-Path $PSScriptRoot "build-app.ps1")

dotnet build .\SmartPerformanceDoctor.sln -c Release -p:Platform=x64 --no-restore 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    dotnet build .\SmartPerformanceDoctor.sln -c Release -p:Platform=x64
    throw "Solution build failed."
}

if (-not $SkipTests) {
    Write-Host "== Unit tests ==" -ForegroundColor Cyan
    dotnet test .\tests\SmartPerformanceDoctor.Tests\SmartPerformanceDoctor.Tests.csproj `
        -c Release -p:Platform=x64 --filter "FullyQualifiedName~AegisMirror" --no-build
    if ($LASTEXITCODE -ne 0) { throw "Aegis unit tests failed." }
}

& (Join-Path $PSScriptRoot "publish-runtime.ps1")
& (Join-Path $PSScriptRoot "verify-runtime.ps1")

if (-not $SkipInstaller) {
    & (Join-Path $PSScriptRoot "build-modular-setup.ps1") -Version $Version
}

Write-Host "" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host " Build complete — v$Version" -ForegroundColor Green
Write-Host " Run: .\PCCare.exe" -ForegroundColor Green
Write-Host " Installer: artifacts\installer\setup\" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green