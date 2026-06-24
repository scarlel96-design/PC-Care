$ErrorActionPreference = "Continue"

Write-Host "== Smart Performance Doctor v44 Regression Suite ==" -ForegroundColor Cyan

$results = @()

function Run-Step($Name, $Command) {
    Write-Host "`n== $Name ==" -ForegroundColor Cyan
    try {
        Invoke-Expression $Command
        $script:results += [PSCustomObject]@{ name = $Name; status = "PASS"; command = $Command; message = "" }
    } catch {
        Write-Host "[FAIL] ${Name}: $_" -ForegroundColor Red
        $script:results += [PSCustomObject]@{ name = $Name; status = "FAIL"; command = $Command; message = "$_" }
    }
}

Run-Step "Source static verification" ".\scripts\verify-source.ps1"
Run-Step "Core selftest" ".\scripts\run-core-smoke.ps1"
Run-Step "RepairHelper base smoke" ".\scripts\run-repairhelper-smoke.ps1"
Run-Step "Runtime layout diagnostics" ".\scripts\run-app-diagnostics.ps1"
Run-Step "Release verification" ".\scripts\verify-release.ps1"
Run-Step "Unit tests" "dotnet test .\tests\SmartPerformanceDoctor.Tests\SmartPerformanceDoctor.Tests.csproj -c Release --no-restore"

$dir = ".\artifacts\rc"
New-Item -ItemType Directory -Path $dir -Force | Out-Null
$results | ConvertTo-Json -Depth 5 | Set-Content "$dir\REGRESSION_RESULTS.json" -Encoding UTF8

Write-Host "[OK] Regression suite completed." -ForegroundColor Green
