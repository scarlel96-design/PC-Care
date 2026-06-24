$ErrorActionPreference = "Stop"

Write-Host "== First-run folder check ==" -ForegroundColor Cyan

$root = "$env:USERPROFILE\Desktop\SmartPerformanceDoctor"
$folders = @("Reports", "RepairLogs", "CrashLogs", "ErrorBundles")

New-Item -ItemType Directory -Path $root -Force | Out-Null
foreach ($folder in $folders) {
    $path = Join-Path $root $folder
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    Write-Host "[OK] $path" -ForegroundColor Green
}
