$ErrorActionPreference = "Stop"

Write-Host "== v38 progress stream verification ==" -ForegroundColor Cyan

$checks = [ordered]@{
    "OperationProgressEvent model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\OperationProgressEvent.cs"
    "OperationProgressSnapshot model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\OperationProgressSnapshot.cs"
    "OperationProgressHub service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\OperationProgressHub.cs"
    "ProgressEventBridge service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\ProgressEventBridgeService.cs"
    "ProgressHudViewModel" = Test-Path ".\src\SmartPerformanceDoctor.App\ViewModels\ProgressHudViewModel.cs"
    "ProgressHudPage" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\ProgressHudPage.xaml"
    "MainWindow progress nav" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\MainWindow.xaml.cs" -Pattern "OpenProgressHud" -Quiet)
    "Dashboard progress binding" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Views\MacDashboardPage.xaml" -Pattern "ProgressEvents" -Quiet)
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
    throw "Progress stream verification failed."
}
