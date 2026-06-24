$ErrorActionPreference = "Stop"

Write-Host "== v36 dashboard binding verification ==" -ForegroundColor Cyan

$checks = [ordered]@{
    "DashboardStatusCard model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\DashboardStatusCard.cs"
    "DashboardAction model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\DashboardAction.cs"
    "Dashboard snapshot model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\DashboardIntelligenceSnapshot.cs"
    "Dashboard service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\DashboardIntelligenceService.cs"
    "Dashboard VM" = Test-Path ".\src\SmartPerformanceDoctor.App\ViewModels\DashboardViewModel.cs"
    "MacDashboardPage" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\MacDashboardPage.xaml"
    "Dashboard binding Summary" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Views\MacDashboardPage.xaml" -Pattern "{Binding Summary}" -Quiet)
    "Dashboard binding Cards" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Views\MacDashboardPage.xaml" -Pattern "Binding Cards" -Quiet)
    "Dashboard binding Actions" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Views\MacDashboardPage.xaml" -Pattern "Binding Actions" -Quiet)
    "Dashboard recent output relocated" = -not (Select-String -Path ".\src\SmartPerformanceDoctor.App\Views\MacDashboardPage.xaml" -Pattern "RecentRecords" -Quiet)
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
    throw "Dashboard binding verification failed."
}
