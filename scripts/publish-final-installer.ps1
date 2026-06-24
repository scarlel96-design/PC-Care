param(
    [string]$Version = "48.0.0"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$CodingWorkRoot = (Resolve-Path (Join-Path $ProjectRoot "..\..")).Path
$SetupDir = Join-Path $ProjectRoot "artifacts\installer\setup"

Write-Host "== 단일 설치 파일 배포 (v$Version) ==" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "build-modular-setup.ps1") -Version $Version

$setupExe = Join-Path $SetupDir "SmartPerformanceDoctor_Setup_v$Version.exe"
if (-not (Test-Path $setupExe)) {
    throw "Setup EXE missing: $setupExe"
}

# 사용자에게 하나만 보이도록 고정 파일명 (ASCII — 한글 파일명은 인코딩 이슈 방지)
$singleName = "PCCare_Setup.exe"
$dest = Join-Path $CodingWorkRoot $singleName
$projectDest = Join-Path $ProjectRoot $singleName

# 이전에 흩뿌려 둔 보조 설치 파일 제거
$legacyNames = @(
    "SmartPerformanceDoctor_Setup_v$Version.exe",
    "SmartPerformanceDoctor_Bundle_v$Version.exe",
    "SmartPerformanceDoctor_v$($Version -replace '\.','_').msi",
    "SmartPerformanceDoctor_v$Version.exe",
    "SmartPerformanceDoctor_INSTALLER_MANIFEST.json",
    "SmartPerformanceDoctor_설치안내.txt"
)
foreach ($name in $legacyNames) {
    $path = Join-Path $CodingWorkRoot $name
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Host "[CLEAN] Removed $name" -ForegroundColor DarkGray
    }
}

Copy-Item $setupExe $dest -Force
Copy-Item $setupExe $projectDest -Force
$hash = (Get-FileHash $dest -Algorithm SHA256).Hash.ToLowerInvariant()

# 코딩 작업 layout 폴더를 최신 45.0.7 빌드로 동기화
$layoutSrc = Join-Path (Split-Path $PSScriptRoot -Parent) "artifacts\installer\layout"
$layoutDest = Join-Path $CodingWorkRoot "layout"
if (Test-Path $layoutSrc) {
    if (Test-Path $layoutDest) { Remove-Item $layoutDest -Recurse -Force }
    Copy-Item $layoutSrc $layoutDest -Recurse -Force
    Write-Host "[OK] Layout synced: $layoutDest" -ForegroundColor Green
}

Write-Host "" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host " 설치 파일 1개: $singleName" -ForegroundColor Green
Write-Host " 위치: $dest" -ForegroundColor Green
Write-Host " SHA256: $hash" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "더블클릭만 하면 됩니다. 다른 exe/msi는 필요 없습니다." -ForegroundColor Cyan