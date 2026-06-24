param(
    [string]$RuntimeDir = ""
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
if (-not $RuntimeDir) {
    $RuntimeDir = Get-RuntimeRoot -ProjectRoot $ProjectRoot
}

$required = @(
    "PCCare.exe",
    "PCCare.deps.json",
    "PCCare.runtimeconfig.json",
    "SmartPerformanceDoctor.exe",
    "SmartPerformanceDoctor.dll",
    "Microsoft.WinUI.dll",
    "engine\smart_performance_doctor_core.exe",
    "engine\smart_performance_doctor_repair_helper.exe",
    "engine\AegisRecoveryHelper.exe",
    "engine\AegisRecoveryService.exe",
    "engine\Microsoft.Extensions.Hosting.dll",
    "content\rules",
    "runtimes\win-x64\native\Microsoft.WindowsAppRuntime.Bootstrap.dll",
    "runtimes\win\lib\net9.0\System.ServiceProcess.ServiceController.dll"
)

Write-Host "== Verify runtime layout ==" -ForegroundColor Cyan
$missing = @()
foreach ($rel in $required) {
    $path = Join-Path $RuntimeDir $rel
    if (-not (Test-Path $path)) {
        $missing += $rel
    }
}

if ($missing.Count -gt 0) {
    throw "런타임 검증 실패 — 누락: $($missing -join ', ')"
}

$appOut = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0"
foreach ($rel in @("Views\UnifiedCarePage.xbf", "Views\ProgramProtectionCenterPage.xbf")) {
    $destPath = Join-Path $RuntimeDir $rel
    $srcPath = Join-Path $appOut $rel
    if (-not (Test-Path $destPath)) {
        throw "UI 리소스 누락: $rel — scripts\publish-runtime.ps1 를 실행하세요."
    }
    if (-not (Test-Path $srcPath)) {
        throw "빌드 출력 UI 누락: $rel — dotnet build -c Release -p:Platform=x64 후 publish-runtime 하세요."
    }
    $destHash = (Get-FileHash -Algorithm SHA256 $destPath).Hash
    $srcHash = (Get-FileHash -Algorithm SHA256 $srcPath).Hash
    if ($destHash -ne $srcHash) {
        throw "UI 리소스가 빌드 출력과 일치하지 않습니다. scripts\publish-runtime.ps1 를 다시 실행하세요: $rel"
    }
}
Write-Host "[OK] Runtime UI resources match build output." -ForegroundColor Green

$exe = Get-Item (Join-Path $RuntimeDir "SmartPerformanceDoctor.exe")
$dll = Get-Item (Join-Path $RuntimeDir "SmartPerformanceDoctor.dll")
if ($exe.Length -lt 65536) { throw "SmartPerformanceDoctor.exe 손상 (크기 $($exe.Length))" }
if ($dll.Length -lt 65536) { throw "SmartPerformanceDoctor.dll 손상 (크기 $($dll.Length))" }

Write-Host "[OK] Runtime integrity verified ($($exe.Length) byte exe)" -ForegroundColor Green
& (Join-Path $PSScriptRoot "verify-runtime-signatures.ps1") -PayloadDir $RuntimeDir
$ridRoot = Join-Path $RuntimeDir "runtimes"
if (Test-Path $ridRoot) {
    $foreign = Get-ChildItem $ridRoot -Directory | Where-Object { $_.Name -notin @("win", "win-x64", "win10-x64", "win11-x64") }
    if ($foreign.Count -gt 0) {
        Write-Host "[WARN] Non-win-x64 RID folders remain: $($foreign.Name -join ', ')" -ForegroundColor Yellow
    } else {
        Write-Host "[OK] Runtime RID layout is win-x64 focused." -ForegroundColor Green
    }
}