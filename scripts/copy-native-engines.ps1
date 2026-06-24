$ErrorActionPreference = "Stop"

$core = ".\target\release\smart_performance_doctor_core.exe"
$helper = ".\target\release\smart_performance_doctor_repair_helper.exe"

$appOutCandidates = @(
    ".\src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0",
    ".\src\SmartPerformanceDoctor.App\bin\Release\net10.0-windows10.0.26100.0"
)

$appOut = $appOutCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $appOut) {
    throw "App 출력 폴더를 찾지 못했습니다."
}

$engineDir = Join-Path $appOut "engine"
New-Item -ItemType Directory -Path $engineDir -Force | Out-Null

if (Test-Path $core) {
    Copy-Item $core $engineDir -Force
    Copy-Item $core (Join-Path $engineDir "AstraCore.exe") -Force
    Write-Host "[OK] copied core -> $engineDir (+ AstraCore.exe alias)" -ForegroundColor Green
} else {
    Write-Host "[WARN] core exe not found: $core" -ForegroundColor Yellow
}

if (Test-Path $helper) {
    Copy-Item $helper $engineDir -Force
    Copy-Item $helper (Join-Path $engineDir "AstraRepairHelper.exe") -Force
    Write-Host "[OK] copied helper -> $engineDir (+ AstraRepairHelper.exe alias)" -ForegroundColor Green
} else {
    Write-Host "[WARN] helper exe not found: $helper" -ForegroundColor Yellow
}