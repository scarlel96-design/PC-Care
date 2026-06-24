$ErrorActionPreference = "Stop"

Write-Host "== Build Rust Core + RepairHelper ==" -ForegroundColor Cyan
cargo build --release

$core = ".\target\release\smart_performance_doctor_core.exe"
$helper = ".\target\release\smart_performance_doctor_repair_helper.exe"

if (-not (Test-Path $core)) {
    throw "Core EXE 생성 실패: $core"
}
if (-not (Test-Path $helper)) {
    throw "RepairHelper EXE 생성 실패: $helper"
}

Write-Host "[OK] Rust native engines built." -ForegroundColor Green
