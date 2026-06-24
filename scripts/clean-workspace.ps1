param(
    [switch]$KeepDist
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")

Write-Host "== Clean workspace (remove stale runtime from project root) ==" -ForegroundColor Cyan

foreach ($name in @("SmartPerformanceDoctor", "AstraCare", "PCCare")) {
    Get-Process $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

Remove-RuntimePublishArtifacts -ProjectRoot $ProjectRoot
Get-ChildItem $ProjectRoot -File -Filter "*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

foreach ($name in @("SHA256SUMS.txt", ".consumer-publish-manifest.json", "apply_pending_deploy.bat", "_pending_deploy")) {
    $path = Join-Path $ProjectRoot $name
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "[CLEAN] $name" -ForegroundColor DarkGray
    }
}

if (-not $KeepDist) {
    $dist = Join-Path $ProjectRoot "dist"
    if (Test-Path $dist) {
        Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "[CLEAN] dist/" -ForegroundColor DarkGray
    }
}

# 개발 중 생성된 중복/임시 폴더
foreach ($dir in @("app", "portable", "installer")) {
    $path = Join-Path $ProjectRoot $dir
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "[CLEAN] legacy $dir/" -ForegroundColor DarkGray
    }
}

Remove-LegacyRuntimeDist -ProjectRoot $ProjectRoot
Write-Host "[OK] Workspace root cleaned. Runtime will publish to project root (PCCare.exe)." -ForegroundColor Green