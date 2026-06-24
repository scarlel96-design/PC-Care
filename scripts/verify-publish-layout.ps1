$ErrorActionPreference = "Stop"

Write-Host "== Verify publish layout ==" -ForegroundColor Cyan

$publish = ".\artifacts\publish\SmartPerformanceDoctor"
if (-not (Test-Path $publish)) {
    throw "publish 폴더가 없습니다. 먼저 .\scripts\publish-local.ps1을 실행하세요."
}

$required = @(
    "smart_performance_doctor_core.exe",
    "smart_performance_doctor_repair_helper.exe"
)

foreach ($item in $required) {
    $path = Join-Path $publish $item
    if (Test-Path $path) {
        Write-Host "[OK] $item" -ForegroundColor Green
    } else {
        Write-Host "[WARN] $item 없음" -ForegroundColor Yellow
    }
}

if (Test-Path (Join-Path $publish "rules")) {
    Write-Host "[OK] rules" -ForegroundColor Green
} else {
    Write-Host "[WARN] rules 폴더 없음" -ForegroundColor Yellow
}

if (Test-Path (Join-Path $publish "assets")) {
    Write-Host "[OK] assets" -ForegroundColor Green
} else {
    Write-Host "[WARN] assets 폴더 없음" -ForegroundColor Yellow
}
