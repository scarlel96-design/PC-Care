$ErrorActionPreference = "Continue"

Write-Host "== Smart Performance Doctor v41 RepairHelper E2E Gate ==" -ForegroundColor Cyan

$out = ".\artifacts\repairhelper-e2e"
New-Item -ItemType Directory -Path $out -Force | Out-Null

$results = @()

function Add-Result($Name, $Status, $Message) {
    $script:results += [PSCustomObject]@{
        name = $Name
        status = $Status
        message = $Message
        timestamp = (Get-Date).ToString("o")
    }
}

function Invoke-Gate($Name, $Command) {
    Write-Host "`n== $Name ==" -ForegroundColor Cyan
    try {
        Invoke-Expression $Command
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
            Add-Result $Name "WARN" "exit=$LASTEXITCODE"
        } else {
            Add-Result $Name "PASS" ""
        }
    } catch {
        Add-Result $Name "FAIL" "$_"
    }
}

Invoke-Gate "Source verification" ".\scripts\verify-source.ps1"
Invoke-Gate "Repair intelligence verification" ".\scripts\verify-repair-intelligence.ps1"
Invoke-Gate "RepairHelper E2E source verification" ".\scripts\verify-repairhelper-e2e-gate.ps1"
Invoke-Gate "Stable log layout verification" ".\scripts\verify-stable-log-layout.ps1"
Invoke-Gate "Windows 11 smoke pack" ".\scripts\run-windows11-smoke-pack.ps1"
Invoke-Gate "Repair evidence sheet" ".\scripts\collect-repair-evidence-sheet.ps1"

if (Get-Command cargo -ErrorAction SilentlyContinue) {
    Invoke-Gate "RepairHelper cargo check" "cargo check --manifest-path .\src\SmartPerformanceDoctor.RepairHelper\Cargo.toml"
} else {
    Add-Result "RepairHelper cargo check" "SKIPPED" "cargo가 설치되어 있지 않습니다."
}

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Invoke-Gate "dotnet restore" "dotnet restore .\src\SmartPerformanceDoctor.Contracts\SmartPerformanceDoctor.Contracts.csproj; dotnet restore .\src\SmartPerformanceDoctor.App\SmartPerformanceDoctor.App.csproj"
} else {
    Add-Result "dotnet restore" "SKIPPED" "dotnet SDK가 설치되어 있지 않습니다."
}

$results | ConvertTo-Json -Depth 6 | Set-Content "$out\E2E_GATE_RESULTS.json" -Encoding UTF8
Write-Host "[OK] E2E gate result: $out\E2E_GATE_RESULTS.json" -ForegroundColor Green
