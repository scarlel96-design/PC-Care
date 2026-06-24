$ErrorActionPreference = "Stop"

Write-Host "== v41 RepairHelper E2E Gate verification ==" -ForegroundColor Cyan

$checks = [ordered]@{
    "RepairHelper E2E model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\RepairHelperE2ECheckItem.cs"
    "RepairHelper E2E summary" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\RepairHelperE2ESummary.cs"
    "Repair root cause signal" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\RepairRootCauseSignal.cs"
    "Repair root cause score" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\RepairRootCauseScore.cs"
    "Repair root cause scoring engine" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\RepairRootCauseScoringEngine.cs"
    "RepairHelper E2E service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\RepairHelperE2EGateService.cs"
    "RepairHelper E2E VM" = Test-Path ".\src\SmartPerformanceDoctor.App\ViewModels\RepairHelperE2EGateViewModel.cs"
    "RepairHelper E2E page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\RepairHelperE2EGatePage.xaml"
    "MainWindow OpenSystem handler" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\MainWindow.xaml.cs" -Pattern "OpenSystem" -Quiet)
    "MainWindow OpenRiskGate handler" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\MainWindow.xaml.cs" -Pattern "OpenRiskGate" -Quiet)
    "MainWindow E2E nav" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\MainWindow.xaml.cs" -Pattern "OpenRepairHelperE2E" -Quiet)
    "RepairHelper rust comma patch" = -not (Select-String -Path ".\src\SmartPerformanceDoctor.RepairHelper\src\main.rs" -Pattern 'timeout_seconds: 240,\s*\}\)\s*"restart_audiosrv"' -Quiet)
    "RepairHelper v41 log string" = (Select-String -Path ".\src\SmartPerformanceDoctor.RepairHelper\src\main.rs" -Pattern "RepairHelper v41" -Quiet)
}

$failed = $false
foreach ($kv in $checks.GetEnumerator()) {
    if ($kv.Value) {
        Write-Host "[OK] $($kv.Key)" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] $($kv.Key)" -ForegroundColor Red
        $failed = $true
    }
}

if ($failed) {
    throw "RepairHelper E2E Gate verification failed."
}
