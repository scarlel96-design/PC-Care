$ErrorActionPreference = "Stop"

Write-Host "== v42 release artifact verification ==" -ForegroundColor Cyan

$publish = ".\artifacts\publish\SmartPerformanceDoctor"
$release = ".\artifacts\release"
$installer = ".\artifacts\installer"

$checks = [ordered]@{
    "Publish folder" = Test-Path $publish
    "Release folder" = Test-Path $release
    "Portable launcher source" = Test-Path ".\portable\start-portable.cmd"
    "Portable layout source" = Test-Path ".\portable\PORTABLE_LAYOUT.json"
    "WiX draft" = Test-Path ".\installer\wix\Product.wxs"
    "MSIX draft" = Test-Path ".\installer\msix\Package.appxmanifest"
    "Package script" = Test-Path ".\scripts\package-portable.ps1"
    "Checksum script" = Test-Path ".\scripts\verify-checksums.ps1"
    "Signing script" = Test-Path ".\scripts\sign-release.ps1"
    "Update channel script" = Test-Path ".\scripts\new-update-channel.ps1"
    "Support bundle script" = Test-Path ".\scripts\collect-error-bundle.ps1"
    "RepairHelper E2E gate" = Test-Path ".\scripts\run-repairhelper-e2e-gate.ps1"
}

$failed = $false
foreach ($kv in $checks.GetEnumerator()) {
    if ($kv.Value) {
        Write-Host "[OK] $($kv.Key)" -ForegroundColor Green
    } else {
        Write-Host "[WARN] $($kv.Key)" -ForegroundColor Yellow
        if ($kv.Key -in @("Portable launcher source","Portable layout source","WiX draft","MSIX draft","Package script","Checksum script","Update channel script")) {
            $failed = $true
        }
    }
}

if ($failed) {
    throw "Release artifact source verification failed."
}
