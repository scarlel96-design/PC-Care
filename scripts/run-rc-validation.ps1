$ErrorActionPreference = "Continue"

$root = Resolve-Path "."
$logDir = Join-Path $root "artifacts\logs"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$log = Join-Path $logDir "rc_validation_$stamp.txt"

Start-Transcript -Path $log -Force

Write-Host "== Smart Performance Doctor v30 RC Validation ==" -ForegroundColor Cyan

$steps = @(
    @{ Name = "Source Verification"; Command = ".\scripts\verify-source.ps1" },
    @{ Name = "Build Diagnostics"; Command = ".\scripts\build-diagnostics.ps1" },
    @{ Name = "Core Smoke"; Command = ".\scripts\run-core-smoke.ps1" },
    @{ Name = "RepairHelper Smoke"; Command = ".\scripts\run-repairhelper-smoke.ps1" },
    @{ Name = "Publish Local"; Command = ".\scripts\publish-local.ps1" },
    @{ Name = "Verify Publish Layout"; Command = ".\scripts\verify-publish-layout.ps1" },
    @{ Name = "Release Manifest"; Command = ".\scripts\new-release-manifest.ps1" },
    @{ Name = "Portable Package"; Command = ".\scripts\package-portable.ps1" },
    @{ Name = "Verify Release"; Command = ".\scripts\verify-release.ps1" },
    @{ Name = "Runtime Layout Diagnostics"; Command = ".\scripts\run-app-diagnostics.ps1" },
    @{ Name = "Prepare Installer Layout"; Command = ".\scripts\prepare-installer-layout.ps1" },
    @{ Name = "Checksum Verification"; Command = ".\scripts\verify-checksums.ps1" },
    @{ Name = "Update Channel Manifest"; Command = ".\scripts\new-update-channel.ps1" }
)

$results = @()

foreach ($step in $steps) {
    Write-Host "`n== $($step.Name) ==" -ForegroundColor Cyan
    try {
        Invoke-Expression $step.Command
        $results += [PSCustomObject]@{ step = $step.Name; status = "PASS"; message = "" }
    } catch {
        Write-Host "[FAIL] $($step.Name): $_" -ForegroundColor Red
        $results += [PSCustomObject]@{ step = $step.Name; status = "FAIL"; message = "$_" }
    }
}

$rcDir = ".\artifacts\rc"
New-Item -ItemType Directory -Path $rcDir -Force | Out-Null
$results | ConvertTo-Json -Depth 5 | Set-Content "$rcDir\RC_VALIDATION_RESULTS.json" -Encoding UTF8

Stop-Transcript

Write-Host "[OK] RC validation log: $log" -ForegroundColor Green
