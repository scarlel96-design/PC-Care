$ErrorActionPreference = "Stop"

Write-Host "== v43 Source Static Verification ==" -ForegroundColor Cyan

$checks = [ordered]@{
    "Cargo workspace" = Test-Path ".\Cargo.toml"
    "WinUI csproj" = Test-Path ".\src\SmartPerformanceDoctor.App\SmartPerformanceDoctor.App.csproj"
    "Contracts project" = Test-Path ".\src\SmartPerformanceDoctor.Contracts\SmartPerformanceDoctor.Contracts.csproj"
    "Rust Core" = Test-Path ".\src\SmartPerformanceDoctor.Core\Cargo.toml"
    "RepairHelper" = Test-Path ".\src\SmartPerformanceDoctor.RepairHelper\Cargo.toml"
    "DISM heartbeat guard" = Test-Path ".\src\SmartPerformanceDoctor.Core\src\engine\dism_guard.rs"
    "Event sink" = Test-Path ".\src\SmartPerformanceDoctor.Core\src\engine\event_sink.rs"
    "Build core script" = Test-Path ".\scripts\build-core.ps1"
    "Build app script" = Test-Path ".\scripts\build-app.ps1"
    "Publish script" = Test-Path ".\scripts\publish-local.ps1"
    "Core smoke script" = Test-Path ".\scripts\run-core-smoke.ps1"
    "Build diagnostics script" = Test-Path ".\scripts\build-diagnostics.ps1"
    "Publish layout script" = Test-Path ".\scripts\verify-publish-layout.ps1"
    "RepairHelper smoke script" = Test-Path ".\scripts\run-repairhelper-smoke.ps1"
    "Repair log page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\RepairLogPage.xaml"
    "Repair log store" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\RepairLogStore.cs"
    "Driver repair page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\DriverRepairPage.xaml"
    "Audio repair page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\AudioRepairPage.xaml"
    "Repair action registry" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\RepairActionRegistry.cs"
    "App diagnostics page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\AppDiagnosticsPage.xaml"
    "App self check service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\AppSelfCheckService.cs"
    "Release manifest script" = Test-Path ".\scripts\new-release-manifest.ps1"
    "Package portable script" = Test-Path ".\scripts\package-portable.ps1"
    "Verify release script" = Test-Path ".\scripts\verify-release.ps1"
    "RC validation script" = Test-Path ".\scripts\run-rc-validation.ps1"
    "Regression suite script" = Test-Path ".\scripts\run-regression-suite.ps1"
    "RC report script" = Test-Path ".\scripts\generate-rc-report.ps1"
    "Release status page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\ReleaseStatusPage.xaml"
    "Quality gate service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\QualityGateService.cs"
    "Error bundle page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\ErrorBundlePage.xaml"
    "Error bundle service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\ErrorBundleService.cs"
    "WiX draft" = (Test-Path ".\artifacts\installer\templates\wix\Product.wxs") -or (Test-Path ".\installer\wix\Product.wxs")
    "MSIX draft" = (Test-Path ".\artifacts\installer\templates\msix\Package.appxmanifest") -or (Test-Path ".\installer\msix\Package.appxmanifest")
    "Installer layout script" = Test-Path ".\scripts\prepare-installer-layout.ps1"
    "Error bundle script" = Test-Path ".\scripts\collect-error-bundle.ps1"
    "Update status page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\UpdateStatusPage.xaml"
    "Update channel service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\UpdateChannelService.cs"
    "Update channel script" = Test-Path ".\scripts\new-update-channel.ps1"
    "Checksum verification script" = Test-Path ".\scripts\verify-checksums.ps1"
    "Sign release script" = Test-Path ".\scripts\sign-release.ps1"
    "Verify signatures script" = Test-Path ".\scripts\verify-signatures.ps1"
    "Release gate script" = Test-Path ".\scripts\run-release-gate.ps1"
    "First run page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\FirstRunPage.xaml"
    "Self healing page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\SelfHealingPage.xaml"
    "Crash log page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\CrashLogPage.xaml"
    "Crash capture service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\CrashCaptureService.cs"
    "Portable launcher" = Test-Path ".\portable\start-portable.ps1"
    "Portable layout repair script" = Test-Path ".\scripts\repair-portable-layout.ps1"
    "Design language script" = Test-Path ".\scripts\verify-design-language.ps1"
    "Mac design tokens" = Test-Path ".\src\SmartPerformanceDoctor.App\Resources\MacDesignTokens.xaml"
    "Mac icon catalog" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\MacIconCatalog.cs"
    "XAML hardening script" = Test-Path ".\scripts\verify-xaml-hardening.ps1"
    "Mac design fallback" = Test-Path ".\src\SmartPerformanceDoctor.App\Resources\MacDesignFallback.xaml"
    "Dashboard metric catalog" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\DashboardMetricCatalog.cs"
    "Dashboard binding script" = Test-Path ".\scripts\verify-dashboard-binding.ps1"
    "Dashboard intelligence service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\DashboardIntelligenceService.cs"
    "Dashboard viewmodel" = Test-Path ".\src\SmartPerformanceDoctor.App\ViewModels\DashboardViewModel.cs"
    "Core dashboard bridge script" = Test-Path ".\scripts\verify-core-dashboard-bridge.ps1"
    "Core dashboard bridge service" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\CoreDashboardBridgeService.cs"
    "Core diagnostic metric" = Test-Path ".\src\SmartPerformanceDoctor.App\Models\CoreDiagnosticMetric.cs"
    "Progress stream script" = Test-Path ".\scripts\verify-progress-stream.ps1"
    "Operation progress hub" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\OperationProgressHub.cs"
    "Progress HUD page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\ProgressHudPage.xaml"
    "Repair intelligence script" = Test-Path ".\scripts\verify-repair-intelligence.ps1"
    "Repair verification engine" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\RepairVerificationEngine.cs"
    "Stable log layout script" = Test-Path ".\scripts\verify-stable-log-layout.ps1"
    "Windows 11 smoke pack" = Test-Path ".\scripts\run-windows11-smoke-pack.ps1"
    "Stable log page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\StableLogLayoutPage.xaml"
    "RepairHelper E2E gate script" = Test-Path ".\scripts\verify-repairhelper-e2e-gate.ps1"
    "RepairHelper E2E page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\RepairHelperE2EGatePage.xaml"
    "Repair root cause scoring engine" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\RepairRootCauseScoringEngine.cs"
    "Release artifact gate script" = Test-Path ".\scripts\run-release-artifact-gate.ps1"
    "Release artifact verification script" = Test-Path ".\scripts\verify-release-artifacts.ps1"
    "Release artifact page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\ReleaseArtifactGatePage.xaml"
    "Final lock script" = Test-Path ".\scripts\run-final-rc2-lock.ps1"
    "Final lock page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\FinalLockPage.xaml"
    "Compile risk scanner" = Test-Path ".\scripts\scan-compile-risk.ps1"
    "Verified repair page" = Test-Path ".\src\SmartPerformanceDoctor.App\Views\VerifiedRepairPage.xaml"
    "Report store" = Test-Path ".\src\SmartPerformanceDoctor.App\Services\ReportStore.cs"
    "Report writer" = Test-Path ".\src\SmartPerformanceDoctor.Core\src\engine\report_writer.rs"
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
    throw "Static verification failed."
}
