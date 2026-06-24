$ErrorActionPreference = "Stop"

Write-Host "== v39 repair verification intelligence verification ==" -ForegroundColor Cyan

$checks = [ordered]@{
    "RepairEvidence model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\RepairEvidence.cs"
    "RepairVerificationResult model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\RepairVerificationResult.cs"
    "IntelligentRepairPlan model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\IntelligentRepairPlan.cs"
    "Repair verification engine" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\RepairVerificationEngine.cs"
    "Repair intelligence service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\DriverAudioSystemRepairIntelligenceService.cs"
    "Repair audit store" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\RepairExecutionAuditStore.cs"
    "Verified repair page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\VerifiedRepairPage.xaml"
    "Verified repair VM" = Test-Path ".\src\SmartPerformanceDoctor.App\ViewModels\VerifiedRepairViewModel.cs"
    "MainWindow verified repair nav" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\MainWindow.xaml.cs" -Pattern "OpenVerifiedRepair" -Quiet)
    "Progress integration" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Services\RepairVerificationEngine.cs" -Pattern "OperationProgressHub" -Quiet)
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
    throw "Repair intelligence verification failed."
}
