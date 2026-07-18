param(
    [string]$PublishDir = "",
    [int]$WaitSeconds = 20
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")

if (-not $PublishDir) {
    $PublishDir = Get-AppPublishOutput -ProjectRoot $ProjectRoot
}

$publishResolved = (Resolve-Path -LiteralPath $PublishDir).Path
$exe = Join-Path $publishResolved "PCCare.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    $exe = Join-Path $publishResolved "SmartPerformanceDoctor.exe"
}
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Publish exe missing: $exe"
}

Write-Host "== Verify publish (no UAC / no elevation) ==" -ForegroundColor Cyan
Write-Host "Publish: $publishResolved"

if (-not (Test-SelfContainedRuntimeLayout $publishResolved)) {
    throw "Self-contained runtime files missing under publish."
}

foreach ($rel in @(
        "Microsoft.UI.Xaml\Themes\themeresources.xbf",
        "Microsoft.UI.Xaml\Themes\generic.xbf",
        "Microsoft.UI.Xaml.pri",
        "MainWindow.xbf",
        "App.xbf",
        "PCCare.exe"
    )) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishResolved $rel))) {
        throw "Publish layout missing: $rel"
    }
}

$staging = Join-Path $env:TEMP ("pccare-layout-verify-{0}" -f ([guid]::NewGuid().ToString("N")))
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$null = robocopy $publishResolved $staging /E /NFL /NDL /NJH /NJS /NC /NS /NP /R:1 /W:1 2>&1
if ($LASTEXITCODE -ge 8) {
    throw "Staging robocopy failed (exit $LASTEXITCODE)."
}

& (Join-Path $PSScriptRoot "sanitize-commercial-layout.ps1") -LayoutDir $staging
& (Join-Path $PSScriptRoot "verify-install-layout.ps1") -LayoutDir $staging
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

$startupLog = Join-Path ([Environment]::GetFolderPath("Desktop")) "SmartPerformanceDoctor\startup.log"
$before = 0
if (Test-Path $startupLog) {
    $before = (Get-Content $startupLog).Count
}

Get-Process SmartPerformanceDoctor, PCCare -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$env:PCCARE_ALLOW_NON_ADMIN_SESSION = "1"
$p = Start-Process -FilePath $exe -WorkingDirectory $publishResolved -PassThru -WindowStyle Normal
Start-Sleep -Seconds $WaitSeconds

$stillRunning = -not $p.HasExited
if ($stillRunning) {
    Write-Host "[OK] Process still alive after ${WaitSeconds}s (PID $($p.Id))" -ForegroundColor Green
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
}
else {
    Write-Host "[WARN] Process exited (code $($p.ExitCode)) — checking startup.log milestones" -ForegroundColor Yellow
}

$newLines = @()
if (Test-Path $startupLog) {
    $newLines = Get-Content $startupLog | Select-Object -Skip $before
    Write-Host "--- startup.log (new lines) ---" -ForegroundColor DarkGray
    $newLines | ForEach-Object { Write-Host $_ }
}

$requiredPhases = @(
    "[initialize-component] ok",
    "[on-launched] main-window-after",
    "[on-launched] ok"
)

$missing = @()
foreach ($phase in $requiredPhases) {
    if (-not ($newLines -match [regex]::Escape($phase))) {
        $missing += $phase
    }
}

if ($missing.Count -gt 0) {
    Write-Host "[FAIL] Missing startup milestones: $($missing -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Publish layout + startup milestones passed (no admin)" -ForegroundColor Green
exit 0