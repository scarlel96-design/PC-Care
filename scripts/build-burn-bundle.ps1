param(
    [string]$Version = "45.0.7",
    [string]$InstallerDir = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
if (-not $InstallerDir) { $InstallerDir = Join-Path $ProjectRoot "artifacts\installer" }

$msiName = "SmartPerformanceDoctor_v$($Version -replace '\.','_').msi"
$msiPath = Join-Path $InstallerDir $msiName
$setupExe = Join-Path $InstallerDir "setup\SmartPerformanceDoctor_Setup_v$Version.exe"

if (-not (Test-Path $msiPath)) {
    Write-Host "[SKIP] MSI not found: $msiPath" -ForegroundColor Yellow
    return
}
if (-not (Test-Path $setupExe)) {
    Write-Host "[SKIP] Setup EXE not found: $setupExe" -ForegroundColor Yellow
    return
}

function Find-Wix {
    $cmd = Get-Command wix -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $toolWix = Join-Path $env:USERPROFILE ".dotnet\tools\wix.exe"
    if (Test-Path $toolWix) { return $toolWix }
    return $null
}

$wix = Find-Wix
if (-not $wix) {
    throw "WiX not installed. Burn bundle build is required for release."
}

$wixDir = Join-Path $ProjectRoot "artifacts\installer\templates\wix"
if (-not (Test-Path (Join-Path $wixDir "Bundle.wxs"))) {
    $wixDir = Join-Path $ProjectRoot "installer\wix"
}
$bundleWxs = Join-Path $wixDir "Bundle.wxs"

$licenseCandidates = @(
    (Join-Path $ProjectRoot "installer\wix\License.rtf"),
    (Join-Path $ProjectRoot "artifacts\installer\templates\wix\License.rtf")
)
$licensePath = $licenseCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $licensePath) {
    throw "License.rtf not found. Expected under installer\wix or artifacts\installer\templates\wix."
}

$bundleOut = Join-Path $InstallerDir "setup\SmartPerformanceDoctor_Bundle_v$Version.exe"
$msiEscaped = $msiPath.Replace('\', '\\')
$setupEscaped = $setupExe.Replace('\', '\\')
$licenseEscaped = $licensePath.Replace('\', '\\')

& $wix extension add -g WixToolset.BootstrapperApplications.wixext 2>&1 | Out-Null
& $wix build -acceptEula wix7 -ext WixToolset.BootstrapperApplications.wixext $bundleWxs `
    -d ProductVersion=$Version -d MsiPath=$msiEscaped -d SetupExePath=$setupEscaped -d LicensePath=$licenseEscaped -o $bundleOut
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $bundleOut)) {
    throw "Burn bundle build failed (exit $LASTEXITCODE)."
}

Write-Host "[OK] Burn bundle: $bundleOut" -ForegroundColor Green