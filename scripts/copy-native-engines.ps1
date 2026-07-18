$ErrorActionPreference = "Stop"

$core = ".\target\release\smart_performance_doctor_core.exe"
$helper = ".\target\release\smart_performance_doctor_repair_helper.exe"

$ProjectRoot = Split-Path $PSScriptRoot -Parent
$appOut = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish"
if (-not (Test-Path -LiteralPath $appOut)) {
    throw "App publish 출력 폴더를 찾지 못했습니다: $appOut"
}

$engineDir = Join-Path $appOut "engine"
New-Item -ItemType Directory -Path $engineDir -Force | Out-Null

if (Test-Path $core) {
    Copy-Item $core $engineDir -Force
    Write-Host "[OK] copied core -> $engineDir" -ForegroundColor Green
} else {
    Write-Host "[WARN] core exe not found: $core" -ForegroundColor Yellow
}

if (Test-Path $helper) {
    Copy-Item $helper $engineDir -Force
    Write-Host "[OK] copied helper -> $engineDir" -ForegroundColor Green
} else {
    Write-Host "[WARN] helper exe not found: $helper" -ForegroundColor Yellow
}