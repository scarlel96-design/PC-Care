$ErrorActionPreference = "Stop"

Write-Host "== v35 XAML hardening verification ==" -ForegroundColor Cyan

$mainWindowXaml = ".\src\SmartPerformanceDoctor.App\MainWindow.xaml"
$mainWindowCs = ".\src\SmartPerformanceDoctor.App\MainWindow.xaml.cs"
$appXaml = ".\src\SmartPerformanceDoctor.App\App.xaml"

$checks = [ordered]@{
    "MacDesignTokens merged" = (Select-String -Path $appXaml -Pattern "MacDesignTokens.xaml" -Quiet)
    "MacDesignFallback merged" = (Select-String -Path $appXaml -Pattern "MacDesignFallback.xaml" -Quiet)
    "MacWindowPanelStyle available" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Resources\MacDesignTokens.xaml" -Pattern "MacWindowPanelStyle" -Quiet)
    "GlassPanelStyle fallback available" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\Resources\MacDesignFallback.xaml" -Pattern "GlassPanelStyle" -Quiet)
    "MainWindow uses controls namespace" = (Select-String -Path $mainWindowXaml -Pattern "SmartPerformanceDoctor.App.Controls" -Quiet)
    "MacNavigationButton control exists" = (Test-Path ".\src\SmartPerformanceDoctor.App\Controls\MacNavigationButton.xaml")
    "MacTrafficLights control exists" = (Test-Path ".\src\SmartPerformanceDoctor.App\Controls\MacTrafficLights.xaml")
}

$handlers = @(
    "OpenQuick",
    "OpenSystem",
    "OpenDriver",
    "OpenAudio",
    "OpenRiskGate",
    "OpenReports",
    "OpenRepairLogs",
    "OpenCrashLogs",
    "OpenAppDiagnostics",
    "OpenReleaseStatus",
    "OpenUpdateStatus",
    "OpenFirstRun",
    "OpenSelfHealing",
    "OpenErrorBundle"
)

foreach ($handler in $handlers) {
    $checks["Handler $handler"] = (Select-String -Path $mainWindowCs -Pattern $handler -Quiet)
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
    throw "XAML hardening verification failed."
}
