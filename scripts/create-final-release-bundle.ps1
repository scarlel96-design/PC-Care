$ErrorActionPreference = "Stop"

Write-Host "== Create final release bundle v43 ==" -ForegroundColor Cyan

$out = ".\artifacts\final-release"
New-Item -ItemType Directory -Path $out -Force | Out-Null

.\scripts\run-final-rc2-lock.ps1
.\scripts\new-release-artifact-manifest.ps1
.\scripts\generate-release-readiness-report.ps1
.\scripts\generate-final-handoff-report.ps1

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$zip = "$out\SmartPerformanceDoctor_v43_FinalReleaseBundle_$stamp.zip"

$items = @(
    ".\artifacts\final-lock",
    ".\artifacts\release-gate",
    ".\artifacts\release",
    ".\docs",
    ".\README.md",
    ".\ENGINE_PACK_MANIFEST_v43.json",
    ".\STABILITY_AUDIT_v43.md",
    ".\SHA256SUMS.txt"
) | Where-Object { Test-Path $_ }

Compress-Archive -Path $items -DestinationPath $zip -Force
Write-Host "[OK] $zip" -ForegroundColor Green
