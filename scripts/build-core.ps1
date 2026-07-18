$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

Write-Host "== Build Rust Core + RepairHelper ==" -ForegroundColor Cyan

$core = ".\target\release\smart_performance_doctor_core.exe"
$helper = ".\target\release\smart_performance_doctor_repair_helper.exe"
$engineSource = ".\src\SmartPerformanceDoctor.Core\src\engine.rs"
$engineModule = ".\src\SmartPerformanceDoctor.Core\src\engine\mod.rs"
$hasEngineSource = (Test-Path $engineSource) -or (Test-Path $engineModule)

function Copy-PrebuiltEngines {
    param([string]$DestinationDir)

    New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
    $existingCore = Join-Path $DestinationDir "smart_performance_doctor_core.exe"
    $existingHelper = Join-Path $DestinationDir "smart_performance_doctor_repair_helper.exe"
    if ((Test-Path $existingCore) -and (Test-Path $existingHelper)) {
        Write-Host "[OK] Native engines already present in $DestinationDir" -ForegroundColor DarkGray
        return $true
    }

    $candidates = @(
        (Join-Path $ProjectRoot "engine\smart_performance_doctor_core.exe"),
        (Join-Path $ProjectRoot "target\release\smart_performance_doctor_core.exe"),
        (Join-Path $env:TEMP "pccare_runtime\engine\smart_performance_doctor_core.exe")
    )
    $helperCandidates = @(
        (Join-Path $ProjectRoot "engine\smart_performance_doctor_repair_helper.exe"),
        (Join-Path $ProjectRoot "target\release\smart_performance_doctor_repair_helper.exe"),
        (Join-Path $env:TEMP "pccare_runtime\engine\smart_performance_doctor_repair_helper.exe")
    )

    $coreSrc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    $helperSrc = $helperCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $coreSrc -or -not $helperSrc) {
        return $false
    }

    $coreDest = (Resolve-Path -LiteralPath (Join-Path $DestinationDir "smart_performance_doctor_core.exe") -ErrorAction SilentlyContinue)?.Path ??
        (Join-Path (Resolve-Path -LiteralPath $DestinationDir).Path "smart_performance_doctor_core.exe")
    $helperDest = (Resolve-Path -LiteralPath (Join-Path $DestinationDir "smart_performance_doctor_repair_helper.exe") -ErrorAction SilentlyContinue)?.Path ??
        (Join-Path (Resolve-Path -LiteralPath $DestinationDir).Path "smart_performance_doctor_repair_helper.exe")
    $coreSrcResolved = (Resolve-Path -LiteralPath $coreSrc).Path
    $helperSrcResolved = (Resolve-Path -LiteralPath $helperSrc).Path
    if (-not $coreSrcResolved.Equals($coreDest, [StringComparison]::OrdinalIgnoreCase)) {
        Copy-Item $coreSrcResolved $coreDest -Force
    }
    if (-not $helperSrcResolved.Equals($helperDest, [StringComparison]::OrdinalIgnoreCase)) {
        Copy-Item $helperSrcResolved $helperDest -Force
    }
    Write-Host "[OK] Reused prebuilt native engines from $coreSrc" -ForegroundColor Yellow
    return $true
}

if ($hasEngineSource) {
    cargo build --release
    if ($LASTEXITCODE -ne 0) {
        throw "cargo build --release failed."
    }
}
else {
    Write-Host "[WARN] Rust engine source missing — reusing prebuilt native engines." -ForegroundColor Yellow
    if (-not (Copy-PrebuiltEngines -DestinationDir ".\target\release")) {
        throw "Prebuilt native engines not found. Restore engine binaries or Rust source."
    }
}

if (-not (Test-Path $core)) {
    if (-not (Copy-PrebuiltEngines -DestinationDir ".\target\release")) {
        throw "Core EXE 생성 실패: $core"
    }
}
if (-not (Test-Path $helper)) {
    if (-not (Copy-PrebuiltEngines -DestinationDir ".\target\release")) {
        throw "RepairHelper EXE 생성 실패: $helper"
    }
}

Write-Host "[OK] Rust native engines ready." -ForegroundColor Green