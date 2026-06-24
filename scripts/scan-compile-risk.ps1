$ErrorActionPreference = "Stop"

Write-Host "== v43 compile-risk scan ==" -ForegroundColor Cyan

$items = @()

function Add-Item($Name, $Status, $Message) {
    $script:items += [PSCustomObject]@{
        name = $Name
        status = $Status
        message = $Message
    }
}

$mainXaml = ".\src\SmartPerformanceDoctor.App\MainWindow.xaml"
$mainCs = ".\src\SmartPerformanceDoctor.App\MainWindow.xaml.cs"

$handlers = Select-String -Path $mainXaml -Pattern 'Click="([^"]+)"' -AllMatches |
    ForEach-Object { $_.Matches } |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique

foreach ($handler in $handlers) {
    if (Select-String -Path $mainCs -Pattern $handler -Quiet) {
        Add-Item "Handler $handler" "PASS" "Found"
    } else {
        Add-Item "Handler $handler" "FAIL" "Missing in MainWindow.xaml.cs"
    }
}

$resourceFiles = @(
    ".\src\SmartPerformanceDoctor.App\Resources\MacDesignTokens.xaml",
    ".\src\SmartPerformanceDoctor.App\Resources\MacDesignFallback.xaml",
    ".\src\SmartPerformanceDoctor.App\Resources\LogLayoutGuard.xaml"
)
foreach ($file in $resourceFiles) {
    Add-Item "Resource $file" ($(if (Test-Path $file) { "PASS" } else { "FAIL" })) "Resource dictionary check"
}

$rust = ".\src\SmartPerformanceDoctor.RepairHelper\src\main.rs"
if (Test-Path $rust) {
    if (Select-String -Path $rust -Pattern 'timeout_seconds: 240,\s*\}\)\s*"restart_audiosrv"' -Quiet) {
        Add-Item "RepairHelper comma risk" "FAIL" "Missing comma before restart_audiosrv"
    } else {
        Add-Item "RepairHelper comma risk" "PASS" "No known comma risk found"
    }
} else {
    Add-Item "RepairHelper rust source" "FAIL" "Missing main.rs"
}

$out = ".\artifacts\final-lock"
New-Item -ItemType Directory -Path $out -Force | Out-Null
$items | ConvertTo-Json -Depth 5 | Set-Content "$out\COMPILE_RISK_SCAN_v43.json" -Encoding UTF8

$failed = @($items | Where-Object { $_.status -eq "FAIL" }).Count
foreach ($item in $items) {
    if ($item.status -eq "PASS") {
        Write-Host "[OK] $($item.name)" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] $($item.name): $($item.message)" -ForegroundColor Red
    }
}

if ($failed -gt 0) {
    throw "Compile-risk scan found $failed issue(s)."
}
