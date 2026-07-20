param(
    [string]$Version = "50.0.0",
    [switch]$SkipBuild
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null
$env:DOTNET_CLI_UI_LANGUAGE = "ko"
$env:PYTHONUTF8 = "1"

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

$TestFilter = "FullyQualifiedName~AegisMirror|FullyQualifiedName~FeatureInstallMapper|FullyQualifiedName~ScopeRepairFilter|FullyQualifiedName~SecureVaultKdf|FullyQualifiedName~SecureVaultPathHelper|FullyQualifiedName~SecurityAttack"
$SubstDrive = "X:"

Write-Host "== PC 케어 프로 v$Version Windows 11 Smoke Pack ==" -ForegroundColor Cyan

$out = Join-Path $ProjectRoot "artifacts\windows11-smoke"
New-Item -ItemType Directory -Path $out -Force | Out-Null

$results = New-Object System.Collections.Generic.List[object]

function Add-Result([string]$Name, [string]$Status, [string]$Message = "") {
    $script:results.Add([PSCustomObject]@{ name = $Name; status = $Status; message = $Message })
    $color = if ($Status -eq "PASS") { "Green" } elseif ($Status -eq "WARN") { "Yellow" } else { "Red" }
    Write-Host "[$Status] $Name" -ForegroundColor $color
    if ($Message) { Write-Host "       $Message" -ForegroundColor DarkGray }
}

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "`n== $Name ==" -ForegroundColor Cyan
    try {
        & $Action
        Add-Result $Name "PASS"
    } catch {
        Add-Result $Name "FAIL" $_.Exception.Message
        throw
    }
}

$vssForbidden = @(
    "Win32_ShadowCopy.Delete",
    "vssadmin delete shadows",
    "Delete Shadows",
    "wmic shadowcopy delete"
)
$vssAllowlist = @(
    "tests\**\*.cs",
    "artifacts\**\*.md",
    "artifacts\**\*.json"
)

try {
    if (-not $SkipBuild) {
        Invoke-Step "Release build (-warnaserror)" {
            dotnet build .\SmartPerformanceDoctor.sln -c Release -p:Platform=x64 -warnaserror
            if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
        }
    }

    Invoke-Step "Private key scan" { & (Join-Path $PSScriptRoot "scan-private-key.ps1") }

    Invoke-Step "Commercial pack generate+sign" {
        & (Join-Path $PSScriptRoot "generate-commercial-packs.ps1") -Version $Version
        & (Join-Path $PSScriptRoot "sign-commercial-packs.ps1") -Version $Version
    }

    Invoke-Step "RC Lock E2E unit tests" {
        dotnet build .\tests\SmartPerformanceDoctor.Tests\SmartPerformanceDoctor.Tests.csproj -c Release -p:Platform=x64
        if ($LASTEXITCODE -ne 0) { throw "dotnet build tests failed ($LASTEXITCODE)" }

        # Long Unicode project paths break WinApp SDK bootstrap DLL load in test host.
        $usedSubst = $false
        try {
            subst $SubstDrive /d 2>$null | Out-Null
            subst $SubstDrive $ProjectRoot
            if ($LASTEXITCODE -eq 0) {
                $usedSubst = $true
                Push-Location "${SubstDrive}\"
            }
            dotnet test .\tests\SmartPerformanceDoctor.Tests\SmartPerformanceDoctor.Tests.csproj `
                -c Release -p:Platform=x64 `
                --filter $TestFilter `
                --no-build
            if ($LASTEXITCODE -ne 0) { throw "dotnet test failed ($LASTEXITCODE)" }
        }
        finally {
            if ($usedSubst) { Pop-Location }
            subst $SubstDrive /d 2>$null | Out-Null
        }
    }

    Invoke-Step "Runtime verify" {
        . (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
        $runtimeRoot = Get-RuntimeRoot -ProjectRoot $ProjectRoot
        if (Test-RuntimePublished $runtimeRoot) {
            & (Join-Path $PSScriptRoot "verify-runtime.ps1") -RuntimeDir $runtimeRoot
        } else {
            throw "PCCare.exe not found under artifacts\runtime — run build.ps1 first"
        }
    }

    Invoke-Step "Policy grep: no VSS auto-delete" {
        $srcFiles = Get-ChildItem -Path ".\src" -Recurse -Include "*.cs" -File
        $hits = @()
        foreach ($pattern in $vssForbidden) {
            $hits += $srcFiles | Select-String -Pattern $pattern -SimpleMatch -ErrorAction SilentlyContinue
        }
        if ($hits.Count -gt 0) {
            $sample = ($hits | Select-Object -First 3 | ForEach-Object { "$($_.Path):$($_.LineNumber)" }) -join "; "
            throw "VSS auto-delete pattern detected: $($hits.Count) hit(s) — $sample"
        }
    }

    Invoke-Step "Rules pack version lock" {
        $packPath = ".\content\data\commercial\rules.pack.json"
        if (-not (Test-Path $packPath)) { throw "rules.pack.json not found" }
        $pack = Get-Content $packPath -Raw | ConvertFrom-Json
        if ($pack.version -ne $Version -and $pack.packVersion -ne $Version) {
            throw "rules.pack version mismatch: expected $Version, got $($pack.version)/$($pack.packVersion)"
        }
    }
} catch {
    Write-Host "[ABORT] Smoke pack failed: $_" -ForegroundColor Red
}

$report = [PSCustomObject]@{
    version = $Version
    generatedAt = (Get-Date).ToString("o")
    passCount = ($results | Where-Object status -eq "PASS").Count
    failCount = ($results | Where-Object status -eq "FAIL").Count
    warnCount = ($results | Where-Object status -eq "WARN").Count
    testFilter = $TestFilter
    steps = $results
}

$jsonPath = Join-Path $out "SMOKE_RESULTS.json"
$report | ConvertTo-Json -Depth 6 | Set-Content $jsonPath -Encoding UTF8

$mdLines = New-Object System.Collections.Generic.List[string]
$mdLines.Add("# PC 케어 프로 v$Version — Windows 11 Smoke Test Report")
$mdLines.Add("")
$mdLines.Add("생성일: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$mdLines.Add("")
$mdLines.Add("| 항목 | 결과 |")
$mdLines.Add("|------|------|")
foreach ($step in $results) {
    $note = if ($step.message) { " — $($step.message)" } else { "" }
    $mdLines.Add("| $($step.name) | $($step.status)$note |")
}
$mdLines.Add("")
$mdLines.Add("## 요약")
$mdLines.Add("")
$mdLines.Add("- PASS: $($report.passCount)")
$mdLines.Add("- FAIL: $($report.failCount)")
$mdLines.Add("- WARN: $($report.warnCount)")
$mdLines.Add("- 테스트 필터: ``$TestFilter``")
$mdLines.Add("")
$mdPath = Join-Path $out "SMOKE_TEST_REPORT.md"
$mdLines | Set-Content $mdPath -Encoding UTF8

if ($report.failCount -gt 0) {
    Write-Host "[FAIL] Smoke pack: $($report.failCount) failure(s)" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Smoke pack completed:" -ForegroundColor Green
Write-Host "     $jsonPath" -ForegroundColor Green
Write-Host "     $mdPath" -ForegroundColor Green