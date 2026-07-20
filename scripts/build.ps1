param(
    [switch]$SkipTests,
    [switch]$SkipInstaller,
    [switch]$SkipSigning
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

$Version = "51.0.0"
Write-Host "== PC 케어 프로 v$Version Build ==" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "check-environment.ps1")
& (Join-Path $PSScriptRoot "scan-private-key.ps1")
& (Join-Path $PSScriptRoot "clean-workspace.ps1") -KeepDist
& (Join-Path $PSScriptRoot "generate-commercial-packs.ps1") -Version $Version
if (-not $SkipSigning) {
    & (Join-Path $PSScriptRoot "sign-commercial-packs.ps1") -Version $Version
}
else {
    Write-Host "[INFO] Signing skipped by request." -ForegroundColor Yellow
}
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

& (Join-Path $PSScriptRoot "publish-runtime.ps1") -SkipSigning:$SkipSigning
& (Join-Path $PSScriptRoot "verify-runtime.ps1") -SkipSignatureCheck:$SkipSigning

$ReleaseRoot = Join-Path (Resolve-Path (Join-Path $ProjectRoot "..\..")).Path "PCCare_Release_v$Version"
$Changelog = Join-Path $ProjectRoot "updates\CHANGELOG_v$Version.json"

if (-not $SkipInstaller) {
    & (Join-Path $PSScriptRoot "build-modular-setup.ps1") -Version $Version -SkipSigning:$SkipSigning
    & (Join-Path $PSScriptRoot "create-update-package.ps1") `
        -FromVersion "50.4.1" `
        -ToVersion $Version `
        -ChangesFile $Changelog `
        -OutputDir $ReleaseRoot
    & (Join-Path $PSScriptRoot "create-pccare-release.ps1") -Version $Version -ReleaseRoot $ReleaseRoot
}

Write-Host "" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host " Build complete — v$Version" -ForegroundColor Green
Write-Host " Run: .\PCCare.exe" -ForegroundColor Green
Write-Host " Installer: artifacts\installer\setup\" -ForegroundColor Green
Write-Host " Release:   $ReleaseRoot\" -ForegroundColor Green
Write-Host "  (Setup + Update — GitHub 릴리즈 업로드용)" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
