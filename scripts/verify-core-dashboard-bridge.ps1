$ErrorActionPreference = "Stop"

Write-Host "== v37 Core dashboard bridge verification ==" -ForegroundColor Cyan

$checks = [ordered]@{
    "CoreDiagnosticMetric model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\CoreDiagnosticMetric.cs"
    "CoreDiagnosticSnapshot model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\CoreDiagnosticSnapshot.cs"
    "Core bridge service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\CoreDashboardBridgeService.cs"
    "Dashboard service uses core bridge" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Services\DashboardIntelligenceService.cs" -Pattern "CoreDashboardBridgeService" -Quiet)
    "Dashboard VM CoreSummary" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\ViewModels\DashboardViewModel.cs" -Pattern "CoreSummary" -Quiet)
    "Dashboard page Core Snapshot" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Views\MacDashboardPage.xaml" -Pattern "Core Snapshot" -Quiet)
    "Core metrics binding" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Views\MacDashboardPage.xaml" -Pattern "CoreMetrics" -Quiet)
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
    throw "Core dashboard bridge verification failed."
}
