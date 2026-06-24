$ErrorActionPreference = "Continue"

Write-Host "== Soak Test Plan Driver ==" -ForegroundColor Cyan

$iterations = 10
if ($env:SPD_SOAK_ITERATIONS) {
    $iterations = [int]$env:SPD_SOAK_ITERATIONS
}

$results = @()

for ($i = 1; $i -le $iterations; $i++) {
    Write-Host "Soak iteration $i / $iterations" -ForegroundColor Cyan
    try {
        .\scripts\run-core-smoke.ps1
        $results += [PSCustomObject]@{ iteration = $i; status = "PASS"; message = "" }
    } catch {
        $results += [PSCustomObject]@{ iteration = $i; status = "FAIL"; message = "$_" }
    }
    Start-Sleep -Seconds 2
}

$dir = ".\artifacts\rc"
New-Item -ItemType Directory -Path $dir -Force | Out-Null
$results | ConvertTo-Json -Depth 5 | Set-Content "$dir\SOAK_RESULTS.json" -Encoding UTF8

Write-Host "[OK] Soak plan completed." -ForegroundColor Green
