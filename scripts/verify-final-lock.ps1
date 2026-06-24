$ErrorActionPreference = "Stop"

Write-Host "== v43 Final Lock verification ==" -ForegroundColor Cyan

$checks = [ordered]@{
    "FinalLockGateItem model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\FinalLockGateItem.cs"
    "FinalLockResult model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\FinalLockResult.cs"
    "FinalRC2LockService" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\FinalRC2LockService.cs"
    "FinalLockViewModel" = Test-Path ".\src\SmartPerformanceDoctor.App\ViewModels\FinalLockViewModel.cs"
    "FinalLockPage" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\FinalLockPage.xaml"
    "MainWindow Final Lock nav" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\MainWindow.xaml.cs" -Pattern "OpenFinalLock" -Quiet)
    "No more work gate doc" = Test-Path ".\docs\NO_MORE_WORK_ACCEPTANCE_GATE_v43.md"
    "Operator guide" = Test-Path ".\docs\FINAL_OPERATOR_GUIDE_v43.md"
    "Final RC2 script" = Test-Path ".\scripts\run-final-rc2-lock.ps1"
    "Compile risk scanner" = Test-Path ".\scripts\scan-compile-risk.ps1"
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
    throw "Final Lock verification failed."
}
