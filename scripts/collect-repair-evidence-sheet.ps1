$ErrorActionPreference = "Continue"

Write-Host "== Collect repair evidence sheet ==" -ForegroundColor Cyan

$out = ".\artifacts\repair-evidence"
New-Item -ItemType Directory -Path $out -Force | Out-Null

$desktopRoot = "$env:USERPROFILE\Desktop\SmartPerformanceDoctor"
$sheet = [ordered]@{
    timestamp = (Get-Date).ToString("o")
    computerName = $env:COMPUTERNAME
    userName = $env:USERNAME
    os = [System.Environment]::OSVersion.ToString()
    repairLogs = @()
    repairAudits = @()
    crashLogs = @()
    reports = @()
    manualChecklist = @(
        "VerifiedRepairPage에서 driver-safe-rescan dry-run 실행",
        "VerifiedRepairPage에서 audio-stack-restart dry-run 실행",
        "RiskAccepted 없이 apply 차단 확인",
        "RiskAccepted 후 apply 시 UAC 확인",
        "RepairAudits JSON 생성 확인",
        "StableLogLayoutPage에서 글자 겹침 없이 스크롤 확인"
    )
}

foreach ($pair in @(
    @{ key="repairLogs"; path="$desktopRoot\RepairLogs"; filter="*" },
    @{ key="repairAudits"; path="$desktopRoot\RepairAudits"; filter="*.json" },
    @{ key="crashLogs"; path="$desktopRoot\CrashLogs"; filter="*" },
    @{ key="reports"; path="$desktopRoot\Reports"; filter="*" }
)) {
    if (Test-Path $pair.path) {
        $sheet[$pair.key] = @(Get-ChildItem $pair.path -File -Recurse -Filter $pair.filter | Select-Object FullName,Length,LastWriteTime)
    }
}

$path = "$out\REPAIR_EVIDENCE_SHEET.json"
$sheet | ConvertTo-Json -Depth 8 | Set-Content $path -Encoding UTF8

Write-Host "[OK] Evidence sheet: $path" -ForegroundColor Green
