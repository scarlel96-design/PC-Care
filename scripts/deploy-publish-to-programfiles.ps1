param(
    [string]$PublishDir = "",
    [string]$TargetDir = "C:\Program Files\PCCare"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")

if (-not $PublishDir) {
    $PublishDir = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish"
}

if (-not (Test-Path -LiteralPath $PublishDir)) {
    throw "Publish folder missing: $PublishDir"
}

$controlsPri = Join-Path $PublishDir "Microsoft.UI.Xaml.Controls.pri"
$mapPri = Join-Path $PublishDir "Microsoft.UI.Xaml.pri"
if ((Test-Path $controlsPri) -and -not (Test-Path $mapPri)) {
    Copy-Item $controlsPri $mapPri -Force
}

foreach ($name in @("PCCare.exe", "SmartPerformanceDoctor.exe")) {
    $p = Join-Path $PublishDir $name
    if (Test-Path $p) {
        $mainExe = $p
        break
    }
}
if (-not $mainExe) {
    throw "No app exe in publish output."
}

Write-Host "== Deploy publish -> $TargetDir ==" -ForegroundColor Cyan
Write-Host "Source: $PublishDir"

foreach ($serviceName in @("AstraCareAegisRecovery", "PCCareAegisRecovery")) {
    $svc = Get-Service $serviceName -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -ne 'Stopped') {
        Write-Host "[INFO] Stopping service: $serviceName" -ForegroundColor Yellow
        Stop-Service $serviceName -Force -ErrorAction SilentlyContinue
    }
}

foreach ($name in @("SmartPerformanceDoctor", "PCCare", "AegisRecoveryService", "AegisRecoveryHelper")) {
    Get-Process $name -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "[INFO] Stopping process: $($_.ProcessName) ($($_.Id))" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
}
Start-Sleep -Seconds 2

$robocopy = Get-Command robocopy -ErrorAction SilentlyContinue
if (-not $robocopy) {
    throw "robocopy required"
}

$null = & robocopy $PublishDir $TargetDir /MIR /NFL /NDL /NJH /NJS /NC /NS /NP /XF *.pdb /R:2 /W:2 2>&1
if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed (exit $LASTEXITCODE)"
}

# Branding aliases expected by installer layout
$spdExe = Join-Path $TargetDir "SmartPerformanceDoctor.exe"
$pccareExe = Join-Path $TargetDir "PCCare.exe"
if (Test-Path $spdExe) {
    Copy-Item $spdExe $pccareExe -Force
    foreach ($pair in @(
            @{ Src = "SmartPerformanceDoctor.deps.json"; Dest = "PCCare.deps.json" },
            @{ Src = "SmartPerformanceDoctor.runtimeconfig.json"; Dest = "PCCare.runtimeconfig.json" },
            @{ Src = "SmartPerformanceDoctor.pri"; Dest = "PCCare.pri" }
        )) {
        $src = Join-Path $TargetDir $pair.Src
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $TargetDir $pair.Dest) -Force
        }
    }
}

$controlsInstalled = Join-Path $TargetDir "Microsoft.UI.Xaml.Controls.pri"
$mapInstalled = Join-Path $TargetDir "Microsoft.UI.Xaml.pri"
if ((Test-Path $controlsInstalled) -and -not (Test-Path $mapInstalled)) {
    Copy-Item $controlsInstalled $mapInstalled -Force
}

Write-Host "[OK] Deployed to $TargetDir" -ForegroundColor Green