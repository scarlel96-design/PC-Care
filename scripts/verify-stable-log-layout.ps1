$ErrorActionPreference = "Stop"

Write-Host "== v40 stable log layout verification ==" -ForegroundColor Cyan

$checks = [ordered]@{
    "LogLayoutGuard resources" = Test-Path ".\src\SmartPerformanceDoctor.App\Resources\LogLayoutGuard.xaml"
    "StableLogEntry model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\StableLogEntry.cs"
    "PreservedExecutionLogStore" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\PreservedExecutionLogStore.cs"
    "StableLogLayoutPage" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\StableLogLayoutPage.xaml"
    "StableLogLayoutPage codebehind" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\StableLogLayoutPage.xaml.cs"
    "Dashboard recent output removed" = -not (Select-String -Path ".\src\SmartPerformanceDoctor.App\Views\MacDashboardPage.xaml" -Pattern "최근 기록" -Quiet)
    "Stable list style" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Resources\LogLayoutGuard.xaml" -Pattern "StableLogListViewStyle" -Quiet)
    "Stable preview line height" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Resources\LogLayoutGuard.xaml" -Pattern "LineHeight" -Quiet)
    "MainWindow stable logs nav" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\MainWindow.xaml.cs" -Pattern "OpenStableLogs" -Quiet)
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
    throw "Stable log layout verification failed."
}
