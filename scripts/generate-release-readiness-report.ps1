$ErrorActionPreference = "Stop"

Write-Host "== Generate release readiness report v42 ==" -ForegroundColor Cyan

$out = ".\artifacts\release-gate"
New-Item -ItemType Directory -Path $out -Force | Out-Null

$gate = "$out\RELEASE_GATE_RESULTS_v42.json"
$manifest = ".\artifacts\release\RELEASE_ARTIFACT_MANIFEST_v42.json"

$lines = @()
$lines += "# Smart Performance Doctor v42 Release Readiness Report"
$lines += ""
$lines += "Generated: $(Get-Date -Format o)"
$lines += ""
$lines += "## Gate Results"
if (Test-Path $gate) {
    $items = Get-Content $gate | ConvertFrom-Json
    foreach ($item in $items) {
        $lines += "- [$($item.status)] $($item.name)"
    }
} else {
    $lines += "- Gate result not found. Run .\scripts\run-release-artifact-gate.ps1"
}

$lines += ""
$lines += "## Artifact Manifest"
if (Test-Path $manifest) {
    $m = Get-Content $manifest | ConvertFrom-Json
    $lines += "- Artifact count: $($m.artifactCount)"
} else {
    $lines += "- Manifest not found. Run .\scripts\new-release-artifact-manifest.ps1"
}

$lines += ""
$lines += "## Manual Final Checks"
$lines += "- Windows 11 app launch"
$lines += "- Stable log scroll overlap check"
$lines += "- VerifiedRepair dry-run check"
$lines += "- RepairHelper E2E gate"
$lines += "- Portable ZIP extraction"
$lines += "- Checksum verification"
$lines += "- Update channel JSON"
$lines += "- Error/support bundle"

$path = "$out\RELEASE_READINESS_REPORT_v42.md"
$lines | Set-Content $path -Encoding UTF8
Write-Host "[OK] $path" -ForegroundColor Green
