$ErrorActionPreference = "Stop"

Write-Host "== Verify release package ==" -ForegroundColor Cyan

$publish = ".\artifacts\publish\SmartPerformanceDoctor"
$release = ".\artifacts\release"
$manifest = Join-Path $release "RELEASE_MANIFEST.json"
$portable = Join-Path $release "SmartPerformanceDoctor_v29_Portable.zip"

if (-not (Test-Path $publish)) { throw "publish 폴더 없음" }
if (-not (Test-Path $manifest)) { throw "RELEASE_MANIFEST.json 없음" }
if (-not (Test-Path $portable)) { throw "portable zip 없음" }

$required = @(
    "smart_performance_doctor_core.exe",
    "smart_performance_doctor_repair_helper.exe"
)

foreach ($item in $required) {
    $path = Join-Path $publish $item
    if (Test-Path $path) {
        Write-Host "[OK] $item" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] $item" -ForegroundColor Red
        throw "필수 파일 누락: $item"
    }
}

if (-not (Test-Path (Join-Path $publish "rules"))) {
    Write-Host "[WARN] rules 폴더가 없습니다." -ForegroundColor Yellow
}

if (-not (Test-Path (Join-Path $publish "assets"))) {
    Write-Host "[WARN] assets 폴더가 없습니다." -ForegroundColor Yellow
}

Write-Host "[OK] Release verification completed." -ForegroundColor Green
