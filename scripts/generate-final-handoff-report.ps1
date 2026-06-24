$ErrorActionPreference = "Stop"

Write-Host "== Generate final handoff report v43 ==" -ForegroundColor Cyan

$out = ".\artifacts\final-lock"
New-Item -ItemType Directory -Path $out -Force | Out-Null

$lines = @()
$lines += "# Smart Performance Doctor v43 Final Handoff Report"
$lines += ""
$lines += "Generated: $(Get-Date -Format o)"
$lines += ""
$lines += "## Final Scope"
$lines += "- Windows 11 performance diagnostics"
$lines += "- Driver/audio/system verified repair"
$lines += "- RepairHelper E2E gate"
$lines += "- macOS-style UI/UX"
$lines += "- Stable preserved log layout"
$lines += "- Portable/WiX/MSIX release gate"
$lines += "- Error/support bundle"
$lines += ""
$lines += "## Required Windows-side Final Commands"
$lines += '```powershell'
$lines += ".\scripts\publish-consumer.ps1"
$lines += ".\scripts\run-regression-suite.ps1"
$lines += '```'
$lines += ""
$lines += "## Manual Final Checks"
$lines += "- App launch"
$lines += "- Dashboard renders"
$lines += "- Stable logs do not overlap during scroll"
$lines += "- VerifiedRepair dry-run/apply gate"
$lines += "- RepairHelper E2E dry-run matrix"
$lines += "- Portable extraction launch"
$lines += "- Checksum/update channel report"
$lines += ""
$lines += "## Known Environment Limitation"
$lines += "This report can be generated from source, but real Windows runtime validation must be performed on Windows 11."

$path = "$out\FINAL_HANDOFF_REPORT_v43.md"
$lines | Set-Content $path -Encoding UTF8
Write-Host "[OK] $path" -ForegroundColor Green
