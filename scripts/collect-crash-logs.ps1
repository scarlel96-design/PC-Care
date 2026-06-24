$ErrorActionPreference = "Stop"

Write-Host "== Collect crash logs ==" -ForegroundColor Cyan

$root = "$env:USERPROFILE\Desktop\SmartPerformanceDoctor\CrashLogs"
$outRoot = ".\artifacts\crash"
New-Item -ItemType Directory -Path $outRoot -Force | Out-Null

if (-not (Test-Path $root)) {
    Write-Host "[WARN] CrashLogs 폴더가 없습니다: $root" -ForegroundColor Yellow
    return
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$zip = Join-Path $outRoot "CrashLogs_$stamp.zip"
Compress-Archive -Path "$root\*" -DestinationPath $zip -Force

Write-Host "[OK] Crash logs archive: $zip" -ForegroundColor Green
