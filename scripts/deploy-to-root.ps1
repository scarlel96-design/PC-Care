param(
    [string]$AppOut = "",
    [int]$WaitSeconds = 120
)

$publishArgs = @{}
if ($AppOut) { $publishArgs.AppOut = $AppOut }

& (Join-Path $PSScriptRoot "publish-runtime.ps1") @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[OK] Runtime deployed to artifacts\runtime. Run: .\artifacts\runtime\PCCare.exe" -ForegroundColor Green
exit 0