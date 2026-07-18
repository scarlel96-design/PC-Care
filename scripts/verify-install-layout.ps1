param(
    [Parameter(Mandatory = $true)]
    [string]$LayoutDir
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")

$layoutResolved = (Resolve-Path -LiteralPath $LayoutDir -ErrorAction Stop).Path
Write-Host "== Verify commercial install layout ==" -ForegroundColor Cyan
Write-Host "Target: $layoutResolved"

$required = @(
    "PCCare.exe",
    "PCCare.deps.json",
    "PCCare.runtimeconfig.json",
    "SmartPerformanceDoctor.dll",
    "coreclr.dll",
    "hostfxr.dll",
    "hostpolicy.dll",
    "FOLDER_LAYOUT.txt",
    "README.txt",
    "engine\smart_performance_doctor_core.exe",
    "engine\smart_performance_doctor_repair_helper.exe",
    "engine\AegisRecoveryHelper.exe",
    "engine\AegisRecoveryService.exe",
    "content",
    "Microsoft.WinUI.dll",
    "Views\UnifiedCarePage.xbf",
    "App.xbf",
    "MainWindow.xbf",
    "Microsoft.UI.Xaml.Controls.pri",
    "Microsoft.UI.Xaml.pri",
    "Microsoft.UI.Xaml\Themes\themeresources.xbf",
    "Microsoft.UI.Xaml\Themes\generic.xbf",
    "Microsoft.WindowsAppRuntime.dll"
)

$missing = @()
foreach ($rel in $required) {
    $path = Join-Path $layoutResolved $rel
    if (-not (Test-Path $path)) {
        $missing += $rel
    }
}

if ($missing.Count -gt 0) {
    throw "설치 레이아웃 누락: $($missing -join ', ')"
}

if (-not (Resolve-WindowsAppBootstrapPath $layoutResolved)) {
    throw "설치 레이아웃 누락: Windows App SDK Bootstrap DLL"
}

$violations = @(Get-InstallLayoutViolations -LayoutDir $layoutResolved)
if ($violations.Count -gt 0) {
    throw "설치 레이아웃 위반:`n$($violations -join "`n")"
}

Write-Host "[OK] Commercial install layout verified." -ForegroundColor Green