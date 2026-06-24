$ErrorActionPreference = "Stop"

Write-Host "== v34 macOS design language verification ==" -ForegroundColor Cyan

$checks = [ordered]@{
    "Mac design tokens" = Test-Path ".\src\SmartPerformanceDoctor.App\Resources\MacDesignTokens.xaml"
    "Mac design fallback" = Test-Path ".\src\SmartPerformanceDoctor.App\Resources\MacDesignFallback.xaml"
    "Mac icon catalog" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\MacIconCatalog.cs"
    "Mac navigation model" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\MacNavigationItem.cs"
    "Traffic lights control" = Test-Path ".\src\SmartPerformanceDoctor.App\Controls\MacTrafficLights.xaml"
    "Navigation button control" = Test-Path ".\src\SmartPerformanceDoctor.App\Controls\MacNavigationButton.xaml"
    "MainWindow mac shell" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\MainWindow.xaml" -Pattern "MacTrafficLights" -Quiet)
    "Sidebar sections" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\MainWindow.xaml" -Pattern "진단" -Quiet)
    "Search field" = (Select-String -Path ".\src\SmartPerformanceDoctor.App\MainWindow.xaml" -Pattern "PlaceholderText" -Quiet)
    "Design docs" = Test-Path ".\docs\MACOS_DESIGN_LANGUAGE_PASS.md"
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
    throw "Design language verification failed."
}
