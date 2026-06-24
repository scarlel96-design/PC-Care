$ErrorActionPreference = "Stop"

Write-Host "== Generate RC report ==" -ForegroundColor Cyan

$rcDir = ".\artifacts\rc"
New-Item -ItemType Directory -Path $rcDir -Force | Out-Null

$validation = Join-Path $rcDir "RC_VALIDATION_RESULTS.json"
$regression = Join-Path $rcDir "REGRESSION_RESULTS.json"

$validationText = if (Test-Path $validation) { Get-Content $validation -Raw } else { "[]" }
$regressionText = if (Test-Path $regression) { Get-Content $regression -Raw } else { "[]" }

$report = @"
# Smart Performance Doctor v30 RC Report

Generated: $(Get-Date -Format o)

## RC Validation Results

````json
$validationText
````

## Regression Results

````json
$regressionText
````

## Manual Verification Required

- Windows 11 WinUI app launch
- UAC runas prompt
- Named Pipe RepairHelper response
- Driver dry-run
- Audio dry-run
- DISM CheckHealth dry-run/apply
- SFC verifyonly dry-run/apply
- DISM RestoreHealth 63% heartbeat behavior
- ReportPage report opening
- RepairLogPage log opening
- Portable ZIP extraction and launch

"@

$path = Join-Path $rcDir "RC_REPORT.md"
$report | Set-Content $path -Encoding UTF8

Write-Host "[OK] RC report: $path" -ForegroundColor Green
