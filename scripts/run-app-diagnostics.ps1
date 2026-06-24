$ErrorActionPreference = "Stop"

Write-Host "== Runtime layout diagnostics ==" -ForegroundColor Cyan

$publish = ".\artifacts\publish\SmartPerformanceDoctor"
if (-not (Test-Path $publish)) {
    throw "publish 폴더가 없습니다."
}

$checks = [ordered]@{
    "Core Engine" = Join-Path $publish "smart_performance_doctor_core.exe"
    "Repair Helper" = Join-Path $publish "smart_performance_doctor_repair_helper.exe"
    "Rules" = Join-Path $publish "rules"
    "Assets" = Join-Path $publish "assets"
    "Docs" = Join-Path $publish "docs"
}

foreach ($kv in $checks.GetEnumerator()) {
    if (Test-Path $kv.Value) {
        Write-Host "[OK] $($kv.Key): $($kv.Value)" -ForegroundColor Green
    } else {
        Write-Host "[MISSING] $($kv.Key): $($kv.Value)" -ForegroundColor Red
    }
}
