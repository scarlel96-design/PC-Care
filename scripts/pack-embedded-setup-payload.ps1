param(
    [string]$LayoutDir,
    [string]$MsiPath,
    [string]$Version = "45.0.7"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$ResourcesDir = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.Setup\Resources"

if (-not (Test-Path $LayoutDir)) {
    throw "Layout directory not found: $LayoutDir"
}

New-Item -ItemType Directory -Path $ResourcesDir -Force | Out-Null

$zipPath = Join-Path $ResourcesDir "installer-layout.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $LayoutDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$msiDest = Join-Path $ResourcesDir "installer.msi"
if (Test-Path $msiDest) { Remove-Item $msiDest -Force }
if ($MsiPath -and (Test-Path $MsiPath)) {
    Copy-Item $MsiPath $msiDest -Force
    Write-Host "[OK] Embedded MSI payload ($Version)" -ForegroundColor Green
} else {
    Write-Host "[WARN] MSI not found — setup will run layout-only install." -ForegroundColor Yellow
}

Write-Host "[OK] Embedded layout zip: $zipPath" -ForegroundColor Green