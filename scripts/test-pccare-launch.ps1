param(
    [string]$ExePath = "",
    [int]$WaitSeconds = 12,
    [switch]$UseElevation
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")

if (-not $ExePath) {
    $publish = Get-AppPublishOutput -ProjectRoot $ProjectRoot
    $candidates = @(
        (Join-Path $publish "PCCare.exe"),
        (Join-Path $publish "SmartPerformanceDoctor.exe"),
        "C:\Program Files\PCCare\PCCare.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) {
            $ExePath = $c
            break
        }
    }
}

if (-not $ExePath -or -not (Test-Path -LiteralPath $ExePath)) {
    throw "PCCare.exe not found. Pass -ExePath or build publish first."
}

$desktopRoot = Join-Path ([Environment]::GetFolderPath("Desktop")) "SmartPerformanceDoctor"
$crashDir = Join-Path $desktopRoot "CrashLogs"
$startupLog = Join-Path $desktopRoot "startup.log"
$beforeCrash = @()
if (Test-Path $crashDir) {
    $beforeCrash = Get-ChildItem $crashDir -Filter "crash_*.log" -File | ForEach-Object { $_.FullName }
}

function Get-PccarePids {
    $names = @("PCCare", "SmartPerformanceDoctor")
    $list = @()
    foreach ($n in $names) {
        $list += Get-Process $n -ErrorAction SilentlyContinue
    }
    return $list | Select-Object -ExpandProperty Id -Unique
}

$beforePids = @(Get-PccarePids)

Write-Host "== PCCare launch test ==" -ForegroundColor Cyan
Write-Host "Exe: $ExePath"
Write-Host "Elevation: $(if ($UseElevation) { 'RunAs' } else { 'normal' })"

if (-not $UseElevation) {
    $env:PCCARE_ALLOW_NON_ADMIN_SESSION = "1"
}

try {
    if ($UseElevation) {
        $null = Start-Process -FilePath $ExePath -Verb RunAs -WindowStyle Normal -ErrorAction Stop
    }
    else {
        $null = Start-Process -FilePath $ExePath -WindowStyle Normal -PassThru -ErrorAction Stop
    }
}
catch {
    Write-Host "[FAIL] Start-Process: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds $WaitSeconds

$running = @(Get-PccarePids | Where-Object { $beforePids -notcontains $_ })
if ($running.Count -eq 0) {
    $running = @(Get-PccarePids)
}

if ($running.Count -gt 0) {
    Write-Host "[OK] PCCare process running (PID $($running -join ', '))" -ForegroundColor Green
    foreach ($procId in $running) {
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    }
}
else {
    Write-Host "[FAIL] No PCCare / SmartPerformanceDoctor process after ${WaitSeconds}s" -ForegroundColor Red
}

if (Test-Path $startupLog) {
    Write-Host "--- startup.log (tail) ---" -ForegroundColor DarkGray
    Get-Content $startupLog -Tail 20 | ForEach-Object { Write-Host $_ }
}

$newCrashes = @()
if (Test-Path $crashDir) {
    $newCrashes = Get-ChildItem $crashDir -Filter "crash_*.log" -File |
        Where-Object { $beforeCrash -notcontains $_.FullName } |
        Sort-Object LastWriteTime -Descending
}

if ($newCrashes.Count -gt 0) {
    Write-Host "--- newest crash ---" -ForegroundColor Yellow
    Get-Content $newCrashes[0].FullName -TotalCount 30 | ForEach-Object { Write-Host $_ }
    exit 1
}

if ($running.Count -eq 0) {
    exit 1
}

exit 0