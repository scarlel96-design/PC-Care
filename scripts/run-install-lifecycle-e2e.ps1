param(
    [string]$Version = "50.0.0",
    [string]$InstallDir = "${env:ProgramFiles}\PCCare",
    [switch]$SkipUninstall
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$Layout = Join-Path $ProjectRoot "artifacts\installer\layout"
$OutDir = Join-Path $ProjectRoot "artifacts\install-lifecycle-e2e"
$ProgramData = Join-Path $env:ProgramData "PCCare"
$script:contractsLoaded = $false
$script:aegisLoaded = $false

function Test-IsAdmin {
    ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-Admin {
    if (Test-IsAdmin) { return }
    throw "Administrator elevation required. Run via scripts\_elevated-rc-lock-runner.ps1 or scripts\run-final-rc-lock-elevated.ps1"
}

function Add-StepResult([string]$Name, [string]$Status, [string]$Message = "") {
    $script:results.Add([PSCustomObject]@{ name = $Name; status = $Status; message = $Message })
    $color = if ($Status -eq "PASS") { "Green" } elseif ($Status -eq "WARN") { "Yellow" } else { "Red" }
    Write-Host "[$Status] $Name" -ForegroundColor $color
    if ($Message) { Write-Host "       $Message" -ForegroundColor DarkGray }
}

function Get-ServiceQuery([string]$Name) {
    $outFile = Join-Path $env:TEMP "sc-query-$Name.txt"
    $p = Start-Process sc.exe -ArgumentList "query $Name" -NoNewWindow -PassThru -Wait -RedirectStandardOutput $outFile -RedirectStandardError "$env:TEMP\sc-err.txt"
    $output = if (Test-Path $outFile) { Get-Content $outFile -Raw } else { "" }
    return @{ Exists = ($p.ExitCode -eq 0); Output = $output }
}

function Stop-AegisServices {
    foreach ($name in @("AstraCareAegisRecovery", "PCCareAegisRecovery")) {
        if ((Get-ServiceQuery $name).Exists) {
            Start-Process sc.exe -ArgumentList "stop $name" -NoNewWindow -Wait | Out-Null
            Start-Sleep -Seconds 2
        }
    }
    Get-Process -Name "AegisRecoveryService" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

function Remove-InstallDirWithRetry([string]$Target, [int]$MaxAttempts = 8) {
    if (-not (Test-Path $Target)) { return }
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        Stop-AegisServices
        try {
            Remove-Item $Target -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq $MaxAttempts) { throw }
            Start-Sleep -Seconds 2
        }
    }
}

function Initialize-ContractsAssembly {
    if ($script:contractsLoaded) { return }
    $path = Join-Path $Layout "SmartPerformanceDoctor.Contracts.dll"
    if (-not (Test-Path $path)) { throw "Missing: $path" }
    [System.Reflection.Assembly]::LoadFrom($path) | Out-Null
    $script:contractsLoaded = $true
}

function Initialize-AegisAssemblies([string]$BaseDir) {
    if ($script:aegisLoaded) { return }
    Initialize-ContractsAssembly
    foreach ($dll in @("SmartPerformanceDoctor.Aegis.dll")) {
        $path = Join-Path $BaseDir $dll
        if (-not (Test-Path $path)) { throw "Missing: $path" }
        [System.Reflection.Assembly]::LoadFrom($path) | Out-Null
    }
    $script:aegisLoaded = $true
}

function New-FeatureManifest([string]$Mode) {
    Initialize-ContractsAssembly
    $installMode = switch ($Mode) {
        "minimal" { [SmartPerformanceDoctor.Contracts.Models.Installation.InstallMode]::Minimal }
        "recommended" { [SmartPerformanceDoctor.Contracts.Models.Installation.InstallMode]::Recommended }
        default { [SmartPerformanceDoctor.Contracts.Models.Installation.InstallMode]::Custom }
    }
    $emptyOptionalIds = [System.Linq.Enumerable]::Empty[string]()
    return [SmartPerformanceDoctor.Contracts.Services.Installation.FeatureCatalog]::CreateManifest($installMode, $Version, $emptyOptionalIds)
}

function Write-InstalledManifest($manifest) {
    New-Item -ItemType Directory -Path $ProgramData -Force | Out-Null
    $opts = [System.Text.Json.JsonSerializerOptions]::new()
    $opts.WriteIndented = $true
    $opts.PropertyNamingPolicy = [System.Text.Json.JsonNamingPolicy]::CamelCase
    $json = [System.Text.Json.JsonSerializer]::Serialize($manifest, $manifest.GetType(), $opts)
    Set-Content (Join-Path $ProgramData "installed_features.json") $json -Encoding UTF8
}

function Copy-LayoutWithManifest($manifest, [string]$Target, [switch]$Incremental) {
    if (-not $Incremental -and (Test-Path $Target)) {
        Remove-Item $Target -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Target -Force | Out-Null
    Initialize-ContractsAssembly
    $copied = 0
    $skipped = 0
    foreach ($file in (Get-ChildItem $Layout -Recurse -File)) {
        $rel = $file.FullName.Substring($Layout.Length).TrimStart('\')
        $name = Split-Path $rel -Leaf
        if ($name -in @("SmartPerformanceDoctor.Setup.exe", "INSTALLER_README.txt")) { continue }
        if (-not [SmartPerformanceDoctor.Contracts.Services.Installation.FeatureInstallMapper]::ShouldInstallRelativePath($rel, $manifest)) {
            $skipped++
            continue
        }
        $dest = Join-Path $Target $rel
        if ($Incremental -and (Test-Path $dest)) { continue }
        New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
        Copy-Item $file.FullName $dest -Force
        $copied++
    }
    return @{ Copied = $copied; Skipped = $skipped }
}

function Invoke-AegisFinalizeInstall([string]$InstallRoot, [string]$Ver) {
    $prev = $env:AEGIS_SIGNING_KEY_PATH
    $devKey = Join-Path $ProjectRoot "artifacts\signing\aegis-dev-private.pem"
    if (Test-Path $devKey) { $env:AEGIS_SIGNING_KEY_PATH = $devKey }
    try {
        # Load from installer layout only — loading from InstallRoot locks DLLs during uninstall.
        Initialize-AegisAssemblies $Layout
        return [SmartPerformanceDoctor.Aegis.AegisPostInstall]::FinalizeInstall($InstallRoot, $Ver)
    }
    finally {
        if ($null -eq $prev) { Remove-Item Env:\AEGIS_SIGNING_KEY_PATH -ErrorAction SilentlyContinue }
        else { $env:AEGIS_SIGNING_KEY_PATH = $prev }
    }
}

function Invoke-AegisFinalizeUninstall {
    Initialize-AegisAssemblies $Layout
    [SmartPerformanceDoctor.Aegis.AegisPostInstall]::FinalizeUninstall()
}

function Repair-FromLayout([string]$Target) {
    $mismatches = 0
    foreach ($file in (Get-ChildItem $Layout -Recurse -File)) {
        $rel = $file.FullName.Substring($Layout.Length).TrimStart('\')
        if ((Split-Path $rel -Leaf) -in @("SmartPerformanceDoctor.Setup.exe", "INSTALLER_README.txt")) { continue }
        $dest = Join-Path $Target $rel
        $needs = -not (Test-Path $dest)
        if (-not $needs) {
            $needs = (Get-FileHash $file.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $dest -Algorithm SHA256).Hash
        }
        if ($needs) {
            New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
            Copy-Item $file.FullName $dest -Force
            $mismatches++
        }
    }
    return $mismatches
}

Ensure-Admin

if (-not (Test-Path $Layout)) {
    throw "Installer layout not found. Run build-modular-setup.ps1 first."
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$results = New-Object System.Collections.Generic.List[object]
$script:e2eFailed = $false

Write-Host "== PC 케어 프로 v$Version Install Lifecycle E2E ==" -ForegroundColor Cyan
Write-Host "Install dir: $InstallDir" -ForegroundColor Cyan
Write-Host "ProgramData: $ProgramData" -ForegroundColor Cyan

$legacyBefore = Get-ServiceQuery "AstraCareAegisRecovery"
Add-StepResult "Pre-check legacy service" $(if ($legacyBefore.Exists) { "PASS" } else { "WARN" }) "AstraCareAegisRecovery exists=$($legacyBefore.Exists)"

try {
    Stop-AegisServices

    $minimal = New-FeatureManifest "minimal"
    $copy = Copy-LayoutWithManifest $minimal $InstallDir
    Write-InstalledManifest $minimal
    $null = Invoke-AegisFinalizeInstall $InstallDir $Version
    Add-StepResult "Minimal install" "PASS" "copied=$($copy.Copied) skipped=$($copy.Skipped)"

    if (-not (Test-Path (Join-Path $InstallDir "PCCare.exe"))) { throw "PCCare.exe missing after minimal install" }
    Add-StepResult "PCCare.exe present" "PASS"

    if ((Get-ServiceQuery "AstraCareAegisRecovery").Exists) { throw "AstraCareAegisRecovery still present after install" }
    Add-StepResult "Legacy service migrated" "PASS"

    $newSvc = Get-ServiceQuery "PCCareAegisRecovery"
    if (-not $newSvc.Exists) { throw "PCCareAegisRecovery not registered" }
    Add-StepResult "PCCareAegisRecovery registered" "PASS"

    if (-not (Test-Path (Join-Path $ProgramData "installed_features.json"))) { throw "installed_features.json missing" }
    Add-StepResult "installed_features.json" "PASS"

    $rulesPack = Join-Path $InstallDir "content\data\commercial\rules.pack.json"
    if (Test-Path $rulesPack) { throw "Minimal install should not include rules.pack.json" }
    Add-StepResult "Minimal skips rules.pack" "PASS"

    $recommended = New-FeatureManifest "recommended"
    $copy2 = Copy-LayoutWithManifest $recommended $InstallDir -Incremental
    Write-InstalledManifest $recommended
    if (-not (Test-Path $rulesPack)) { throw "rules.pack missing after modify to recommended" }
    Add-StepResult "Modify to recommended" "PASS" "added=$($copy2.Copied)"

    $exe = Join-Path $InstallDir "PCCare.exe"
    $goodBytes = [System.IO.File]::ReadAllBytes($exe)
    [System.IO.File]::WriteAllText($exe, "CORRUPT")
    $fixed = Repair-FromLayout $InstallDir
    $null = Invoke-AegisFinalizeInstall $InstallDir $Version
    $restored = [System.Collections.StructuralComparisons]::StructuralEqualityComparer.Equals(
        [System.IO.File]::ReadAllBytes($exe), $goodBytes)
    if (-not $restored) { throw "Repair did not restore PCCare.exe" }
    Add-StepResult "Repair restores damaged EXE" "PASS" "fixed=$fixed"

    $fv = (Get-Item $exe).VersionInfo.ProductVersion
    Add-StepResult "Installed product version" $(if ($fv -like "50*") { "PASS" } else { "WARN" }) "ProductVersion=$fv"

    if (-not $SkipUninstall) {
        Stop-AegisServices
        Invoke-AegisFinalizeUninstall
        Stop-AegisServices
        Remove-InstallDirWithRetry $InstallDir
        if ((Get-ServiceQuery "PCCareAegisRecovery").Exists) { throw "PCCareAegisRecovery still present after uninstall" }
        Add-StepResult "Uninstall removes service" "PASS"
        Add-StepResult "Uninstall removes Program Files" $(if (-not (Test-Path $InstallDir)) { "PASS" } else { "FAIL" })
    } else {
        Add-StepResult "Uninstall" "WARN" "skipped"
    }
}
catch {
    Add-StepResult "Install lifecycle E2E" "FAIL" $_.Exception.Message
    $script:e2eFailed = $true
}

$report = [PSCustomObject]@{
    version = $Version
    generatedAt = (Get-Date).ToString("o")
    installDir = $InstallDir
    programData = $ProgramData
    passCount = ($results | Where-Object status -eq "PASS").Count
    failCount = ($results | Where-Object status -eq "FAIL").Count
    warnCount = ($results | Where-Object status -eq "WARN").Count
    steps = $results
}
$report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutDir "INSTALL_LIFECYCLE_E2E.json") -Encoding UTF8

$md = @(
    "# PC 케어 프로 v$Version — Install Lifecycle E2E",
    "",
    "생성일: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
    "",
    "| 단계 | 결과 | 비고 |",
    "|------|------|------|"
)
foreach ($r in $results) {
    $note = if ($r.message) { $r.message } else { "" }
    $md += "| $($r.name) | $($r.status) | $note |"
}
$md += ""
$md += "## 요약: PASS $($report.passCount) / FAIL $($report.failCount) / WARN $($report.warnCount)"
$md | Set-Content (Join-Path $OutDir "INSTALL_LIFECYCLE_E2E.md") -Encoding UTF8

if ($report.failCount -gt 0 -or $script:e2eFailed) {
    Write-Host "[FAIL] Install lifecycle E2E: $($report.failCount) failure(s)" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Install lifecycle E2E completed: $OutDir" -ForegroundColor Green