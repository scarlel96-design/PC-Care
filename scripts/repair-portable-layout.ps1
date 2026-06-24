$ErrorActionPreference = "Continue"

Write-Host "== Repair portable layout ==" -ForegroundColor Cyan

$publish = ".\artifacts\publish\SmartPerformanceDoctor"
if (-not (Test-Path $publish)) {
    New-Item -ItemType Directory -Path $publish -Force | Out-Null
}

$native = ".\target\release"
$core = Join-Path $native "smart_performance_doctor_core.exe"
$helper = Join-Path $native "smart_performance_doctor_repair_helper.exe"

if (Test-Path $core) {
    Copy-Item $core (Join-Path $publish "smart_performance_doctor_core.exe") -Force
    Write-Host "[OK] Core copied" -ForegroundColor Green
} else {
    Write-Host "[WARN] Core source not found: $core" -ForegroundColor Yellow
}

if (Test-Path $helper) {
    Copy-Item $helper (Join-Path $publish "smart_performance_doctor_repair_helper.exe") -Force
    Write-Host "[OK] RepairHelper copied" -ForegroundColor Green
} else {
    Write-Host "[WARN] RepairHelper source not found: $helper" -ForegroundColor Yellow
}

foreach ($folder in @("rules", "assets", "docs")) {
    if (Test-Path ".\$folder") {
        Copy-Item ".\$folder" (Join-Path $publish $folder) -Recurse -Force
        Write-Host "[OK] $folder copied" -ForegroundColor Green
    }
}

Copy-Item ".\portable\start-portable.ps1" $publish -Force
Copy-Item ".\portable\start-portable.cmd" $publish -Force
Copy-Item ".\portable\PORTABLE_LAYOUT.json" $publish -Force

Write-Host "[OK] Portable layout repair completed." -ForegroundColor Green
