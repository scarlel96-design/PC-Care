$ErrorActionPreference = "Stop"

Write-Host "== RepairHelper Smoke ==" -ForegroundColor Cyan

$helperCandidates = @(
    ".\target\release\smart_performance_doctor_repair_helper.exe",
    ".\src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\smart_performance_doctor_repair_helper.exe",
    ".\src\SmartPerformanceDoctor.App\bin\Release\net10.0-windows10.0.26100.0\smart_performance_doctor_repair_helper.exe"
)

$helper = $helperCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $helper) {
    throw "RepairHelper EXE를 찾지 못했습니다. 먼저 .\scripts\build-core.ps1을 실행하세요."
}

$output = & $helper
$output

if ($output -notmatch '"status"') {
    throw "RepairHelper base smoke failed."
}

Write-Host "[OK] RepairHelper base smoke completed." -ForegroundColor Green
