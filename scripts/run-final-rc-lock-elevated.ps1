param(
    [string]$Version = "50.0.0",
    [switch]$SkipBuild,
    [switch]$SkipHandoff
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent

function Test-IsAdmin {
    ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdmin)) {
    $runner = Join-Path $PSScriptRoot "_elevated-rc-lock-runner.ps1"
    $elevate = "C:\Program Files\Mullvad VPN\resources\elevate.exe"
    if (Test-Path $elevate) {
        & $elevate -wait pwsh "-NoProfile -ExecutionPolicy Bypass -File `"$runner`" -Version $Version"
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } else {
        & (Join-Path $PSScriptRoot "invoke-elevated-once.ps1") -ScriptPath $runner -ScriptArgs @("-Version", $Version) -TimeoutSec 2400
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

Set-Location $ProjectRoot

if (-not (Test-IsAdmin)) {
    # Non-elevated portion after scheduled task
    $gateArgs = @{ Version = $Version; SkipInstaller = $true; SkipElevatedSteps = $true }
    if ($SkipBuild) { $gateArgs.SkipBuild = $true }
    if ($SkipHandoff) { $gateArgs.SkipHandoff = $true }
    & (Join-Path $PSScriptRoot "run-v50-rc-lock-gate.ps1") @gateArgs
    exit $LASTEXITCODE
}

$gateArgs = @{ Version = $Version }
if ($SkipBuild) { $gateArgs.SkipBuild = $true }
if ($SkipHandoff) { $gateArgs.SkipHandoff = $true }
& (Join-Path $PSScriptRoot "run-v50-rc-lock-gate.ps1") @gateArgs
exit $LASTEXITCODE