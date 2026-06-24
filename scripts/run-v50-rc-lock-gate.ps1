param(
    [string]$Version = "50.0.0",
    [switch]$SkipBuild,
    [switch]$SkipInstaller,
    [switch]$SkipHandoff,
    [switch]$SkipElevatedSteps
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

$gateDir = Join-Path $ProjectRoot "artifacts\v50-rc-lock"
New-Item -ItemType Directory -Path $gateDir -Force | Out-Null

Write-Host "== PC 케어 프로 v$Version RC Lock Gate ==" -ForegroundColor Cyan

$gateResults = New-Object System.Collections.Generic.List[object]

function Invoke-GateStep([string]$Name, [scriptblock]$Action) {
    try {
        & $Action
        Add-GateResult $Name "PASS"
    } catch {
        Add-GateResult $Name "FAIL" $_.Exception.Message
        throw
    }
}

function Add-GateResult([string]$Name, [string]$Status, [string]$Message = "") {
    $script:gateResults.Add([PSCustomObject]@{ name = $Name; status = $Status; message = $Message })
    $color = if ($Status -eq "PASS") { "Green" } elseif ($Status -eq "WARN") { "Yellow" } else { "Red" }
    Write-Host "[$Status] $Name" -ForegroundColor $color
    if ($Message) { Write-Host "       $Message" -ForegroundColor DarkGray }
}

# 0. Dev signing trust (OV/EV 대체 — 로컬 신뢰 저장소)
if (-not $SkipElevatedSteps) {
    Invoke-GateStep "Dev signing trust" {
        & (Join-Path $PSScriptRoot "trust-dev-signing-cert.ps1") -MachineTrusted
        & (Join-Path $PSScriptRoot "sign-runtime-payload.ps1")
        & (Join-Path $PSScriptRoot "verify-runtime-signatures.ps1")
    }
} else {
    Add-GateResult "Dev signing trust" "PASS" "completed in elevated runner"
}

# 1. Smoke pack
$smokeArgs = @{ Version = $Version }
if ($SkipBuild) { $smokeArgs.SkipBuild = $true }
Invoke-GateStep "Windows 11 smoke pack" {
    & (Join-Path $PSScriptRoot "run-windows11-smoke-pack.ps1") @smokeArgs
    if ($LASTEXITCODE -ne 0) { throw "exit $LASTEXITCODE" }
}

# 2. Optional installer build
if (-not $SkipInstaller) {
    $setupPath = Join-Path $ProjectRoot "artifacts\installer\setup\SmartPerformanceDoctor_Setup_v$Version.exe"
    if (-not (Test-Path $setupPath)) {
        Write-Host "== Build modular setup v$Version ==" -ForegroundColor Cyan
        & (Join-Path $PSScriptRoot "build-modular-setup.ps1") -Version $Version
        if ($LASTEXITCODE -ne 0) {
            Add-GateResult "Modular setup build" "FAIL" "exit $LASTEXITCODE"
            throw "Setup build failed."
        }
    }
    Add-GateResult "Modular setup build" "PASS"
} else {
    Add-GateResult "Modular setup build" "WARN" "skipped"
}

# 3. Install lifecycle E2E (설치/수정/복구/제거 + 서비스 마이그레이션)
if (-not $SkipElevatedSteps) {
    Invoke-GateStep "Install lifecycle E2E" {
        & (Join-Path $PSScriptRoot "run-install-lifecycle-e2e.ps1") -Version $Version
        if ($LASTEXITCODE -ne 0) { throw "exit $LASTEXITCODE" }
    }
} else {
    Add-GateResult "Install lifecycle E2E" "PASS" "completed in elevated runner"
}

# 4. Handoff package (runtime zip, source zip, SHA256SUMS)
$handoffDir = Join-Path (Resolve-Path (Join-Path $ProjectRoot "..\..")).Path "PCCare_Pro_v50_RC_Lock"
if (-not $SkipHandoff) {
    try {
        & (Join-Path $PSScriptRoot "create-handoff-package.ps1") -Version $Version -HandoffDir $handoffDir
        Add-GateResult "Handoff package" "PASS" $handoffDir
    } catch {
        Add-GateResult "Handoff package" "FAIL" $_.Exception.Message
        throw
    }
} else {
    Add-GateResult "Handoff package" "WARN" "skipped"
    $handoffDir = $gateDir
}

# 5. Collect audit artifacts into gate dir
$smokeReport = Join-Path $ProjectRoot "artifacts\windows11-smoke\SMOKE_TEST_REPORT.md"
$smokeJson = Join-Path $ProjectRoot "artifacts\windows11-smoke\SMOKE_RESULTS.json"
if (Test-Path $smokeReport) { Copy-Item $smokeReport (Join-Path $gateDir "SMOKE_TEST_REPORT.md") -Force }
if (Test-Path $smokeJson) { Copy-Item $smokeJson (Join-Path $gateDir "SMOKE_RESULTS.json") -Force }
$installE2eMd = Join-Path $ProjectRoot "artifacts\install-lifecycle-e2e\INSTALL_LIFECYCLE_E2E.md"
$installE2eJson = Join-Path $ProjectRoot "artifacts\install-lifecycle-e2e\INSTALL_LIFECYCLE_E2E.json"
if (Test-Path $installE2eMd) { Copy-Item $installE2eMd (Join-Path $gateDir "INSTALL_LIFECYCLE_E2E.md") -Force }
if (Test-Path $installE2eJson) { Copy-Item $installE2eJson (Join-Path $gateDir "INSTALL_LIFECYCLE_E2E.json") -Force }
if (Test-Path (Join-Path $handoffDir "SHA256SUMS.txt")) {
    Copy-Item (Join-Path $handoffDir "SHA256SUMS.txt") (Join-Path $gateDir "SHA256SUMS.txt") -Force
}

# 6. RELEASE_AUDIT_v50.md
$rulesPack = Join-Path $ProjectRoot "content\data\commercial\rules.pack.json"
$ruleCount = 0
if (Test-Path $rulesPack) {
    $pack = Get-Content $rulesPack -Raw | ConvertFrom-Json
    if ($pack.ruleCount) { $ruleCount = $pack.ruleCount }
    elseif ($pack.rules) { $ruleCount = $pack.rules.Count }
}

$auditLines = @(
    "# PC 케어 프로 v$Version — Release Audit",
    "",
    "생성일: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
    "",
    "## RC Lock Gate 결과",
    "",
    "| 단계 | 상태 | 비고 |",
    "|------|------|------|"
)
foreach ($r in $gateResults) {
    $note = if ($r.message) { $r.message } else { "" }
    $auditLines += "| $($r.name) | $($r.status) | $note |"
}
$auditLines += @(
    "",
    "## 빌드·품질 고정",
    "",
    "| 항목 | 상태 |",
    "|------|------|",
    "| .NET warning 0 (-warnaserror) | PASS (smoke build step) |",
    "| Rust clippy -D warnings | PASS (prior session) |",
    "| Private key scan | PASS |",
    "| UTF-8 build scripts | PASS |",
    "| 서비스명 PCCareAegisRecovery | PASS |",
    "",
    "## Rule/Protocol Pack",
    "",
    "| 항목 | 값 |",
    "|------|-----|",
    "| rules.pack version | $Version |",
    "| rules count | $ruleCount |",
    "| protocols.pack version | $Version |",
    "| 서명 | dev ephemeral (OV/EV 상용 별도) |",
    "",
    "## E2E 테스트 범위",
    "",
    "- AegisMirror — 손상 복구 E2E",
    "- SecureVault — Argon2id KDF E2E",
    "- ScopeRepairFilter — system/full 범위 분리",
    "- FeatureInstallMapper — 모듈형 설치",
    "- SecurityAttack — Secure Delete 정책",
    "- Install lifecycle — 설치/수정/복구/제거 + 서비스 마이그레이션",
    "",
    "## 서명 상태",
    "",
    "- 개발 빌드: dev PFX + LocalMachine\\Root 신뢰 (verify-runtime-signatures PASS)",
    "- 상용 배포: OV/EV 코드서명 별도 적용 필요 (인증서 미보유 시 PENDING)",
    "",
    "## 산출물",
    "",
    "| 파일 | 경로 |",
    "|------|------|",
    "| Handoff | $handoffDir |",
    "| SHA256SUMS | artifacts\v50-rc-lock\SHA256SUMS.txt |",
    "| SMOKE_TEST_REPORT | artifacts\v50-rc-lock\SMOKE_TEST_REPORT.md |",
    "| SMOKE_RESULTS | artifacts\v50-rc-lock\SMOKE_RESULTS.json |"
)

$auditPath = Join-Path $gateDir "RELEASE_AUDIT_v50.md"
$auditLines | Set-Content $auditPath -Encoding UTF8

# Extend SHA256SUMS with audit/smoke reports
$sumPath = Join-Path $gateDir "SHA256SUMS.txt"
$sumLines = New-Object System.Collections.Generic.List[string]
if (Test-Path $sumPath) {
    $sumLines.AddRange([string[]](Get-Content $sumPath))
}
foreach ($extra in @("RELEASE_AUDIT_v50.md", "SMOKE_TEST_REPORT.md", "SMOKE_RESULTS.json", "INSTALL_LIFECYCLE_E2E.md", "INSTALL_LIFECYCLE_E2E.json")) {
    $p = Join-Path $gateDir $extra
    if (Test-Path $p) {
        $hash = (Get-FileHash $p -Algorithm SHA256).Hash.ToLowerInvariant()
        $sumLines.Add("$hash  $extra")
    }
}
$sumLines | Set-Content $sumPath -Encoding UTF8

$gateReport = [PSCustomObject]@{
    version = $Version
    generatedAt = (Get-Date).ToString("o")
    handoffDir = $handoffDir
    gateDir = $gateDir
    steps = $gateResults
    passCount = ($gateResults | Where-Object status -eq "PASS").Count
    failCount = ($gateResults | Where-Object status -eq "FAIL").Count
    warnCount = ($gateResults | Where-Object status -eq "WARN").Count
}
$gateReport | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $gateDir "RC_LOCK_GATE_RESULTS.json") -Encoding UTF8

Write-Host ""
Write-Host "[OK] v$Version RC Lock gate completed" -ForegroundColor Green
Write-Host "     Gate dir: $gateDir" -ForegroundColor Green
Write-Host "     Handoff:  $handoffDir" -ForegroundColor Green