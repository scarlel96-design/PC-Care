param(
    [string]$Version = "50.0.0",
    [string[]]$Targets = @(),
    [switch]$SkipIfNoCert
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$setupDir = Join-Path $ProjectRoot "artifacts\installer\setup"

if (-not (Test-Path $setupDir)) {
    Write-Host "[SKIP] Installer setup folder not found." -ForegroundColor Yellow
    return
}

$env:SPD_SIGN_CERT_PATH = if ($env:SPD_SIGN_CERT_PATH) {
    $env:SPD_SIGN_CERT_PATH
} else {
    Join-Path $ProjectRoot "artifacts\signing\SmartPerformanceDoctor-dev.pfx"
}

if (-not $env:SPD_SIGN_CERT_PASSWORD) {
    $env:SPD_SIGN_CERT_PASSWORD = "SmartPerformanceDoctor-Dev-2026"
}

$primaryName = "PCCare_Setup_v$Version.exe"
$legacyName = "SmartPerformanceDoctor_Setup_v$Version.exe"
$targets = if ($Targets.Count -gt 0) {
    $Targets | Where-Object { Test-Path $_ }
} else {
    @(
        (Join-Path $setupDir $primaryName),
        (Join-Path $setupDir $legacyName)
    ) | Where-Object { Test-Path $_ }
}

if ($targets.Count -eq 0) {
    Write-Host "[SKIP] No setup EXE to sign in $setupDir" -ForegroundColor Yellow
    return
}

function Find-SignTool {
    $cmd = Get-Command signtool -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $kits) {
        $found = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

$signtool = Find-SignTool
$certPath = $env:SPD_SIGN_CERT_PATH
if (-not $signtool -or -not (Test-Path $certPath)) {
    if ($SkipIfNoCert) {
        Write-Host "[SKIP] signtool or certificate not available." -ForegroundColor Yellow
        return
    }
    throw "Signing prerequisites missing."
}

$timestampUrl = if ($env:SPD_TIMESTAMP_URL) { $env:SPD_TIMESTAMP_URL } else { "http://timestamp.digicert.com" }
foreach ($target in $targets) {
    Write-Host "Signing $(Split-Path $target -Leaf)" -ForegroundColor Gray
    & $signtool sign /fd SHA256 /f $certPath /p $env:SPD_SIGN_CERT_PASSWORD /tr $timestampUrl /td SHA256 $target
}

Write-Host "[OK] Signed $($targets.Count) setup artifact(s)." -ForegroundColor Green