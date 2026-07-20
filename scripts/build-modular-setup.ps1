param(
    [string]$Version = "51.0.2",
    [switch]$SkipAppBuild,
    [switch]$SkipMsi,
    [switch]$SkipSigning
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

Write-Host "== Modular Setup Build (v$Version) ==" -ForegroundColor Cyan

. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
$appOut = Get-AppPublishOutput -ProjectRoot $ProjectRoot
$setupOut = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.Setup\bin\x64\Release\net10.0-windows"
$installerDir = Join-Path $ProjectRoot "artifacts\installer"
$setupDir = Join-Path $installerDir "setup"
$layout = Join-Path $installerDir "layout"

if (-not $SkipAppBuild) {
    Write-Host "Building App + Setup (Release x64)..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "build-core.ps1")
    & (Join-Path $PSScriptRoot "build-app.ps1")
    & (Join-Path $PSScriptRoot "publish-runtime.ps1") -SkipSigning:$SkipSigning
    & (Join-Path $PSScriptRoot "verify-runtime.ps1") -SkipSignatureCheck:$SkipSigning
}

if (-not (Test-Path $appOut)) {
    throw "App output not found: $appOut"
}
if (-not (Test-Path $setupOut)) {
    throw "Setup output not found: $setupOut"
}

. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
$runtimeRoot = Get-RuntimeRoot -ProjectRoot $ProjectRoot
if (Test-SelfContainedRuntimeLayout $appOut) {
    $layoutSource = $appOut
}
elseif (Test-SelfContainedRuntimeLayout $runtimeRoot) {
    $layoutSource = $runtimeRoot
}
elseif (Test-RuntimePublished $runtimeRoot) {
    $layoutSource = $runtimeRoot
}
else {
    $layoutSource = $appOut
}
& (Join-Path $PSScriptRoot "prepare-installer-layout.ps1") -SourceDir $layoutSource
& (Join-Path $PSScriptRoot "trim-runtime-rid.ps1") -RuntimeDir $layout
if (-not $SkipSigning) {
    & (Join-Path $PSScriptRoot "sign-runtime-payload.ps1") -PayloadDir $layout
    try {
        & (Join-Path $PSScriptRoot "trust-dev-signing-cert.ps1")
        Write-Host "[OK] Dev signing certificate trusted for Authenticode verification." -ForegroundColor Green
    }
    catch {
        Write-Host "[WARN] Dev signing trust step failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}
else {
    Write-Host "[INFO] Runtime signing and certificate trust skipped by request." -ForegroundColor Yellow
}

$setupExe = Join-Path $setupOut "SmartPerformanceDoctor.Setup.exe"
if (-not (Test-Path $setupExe)) {
    throw "Setup EXE not found: $setupExe"
}

New-Item -ItemType Directory -Path $setupDir -Force | Out-Null
# 동일 버전 설치 산출물만 교체 (50.1.0 / 50.1.1 등 병행 배포 유지)
$bundleNameToReplace = "PCCare_Setup_v$Version.exe"
Get-ChildItem $setupDir -File -Filter $bundleNameToReplace -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue
$msiToken = $Version -replace '\.', '_'
Get-ChildItem $installerDir -Filter "SmartPerformanceDoctor_v*.msi" -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne "SmartPerformanceDoctor_v$msiToken.msi" } |
    Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem $installerDir -Filter "SmartPerformanceDoctor_Bundle_v*.exe" -File -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

$bundleName = "PCCare_Setup_v$Version.exe"
$bundlePath = Join-Path $setupDir $bundleName
Copy-Item $setupExe $bundlePath -Force

if (-not $SkipMsi) {
    & (Join-Path $PSScriptRoot "build-wix-msi.ps1") -Version $Version -SourceDir $layout -OutputDir $installerDir
}

$msiFile = Get-ChildItem $installerDir -Filter "SmartPerformanceDoctor_v*.msi" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$msiPath = if ($msiFile) { $msiFile.FullName } else { "" }
if ($msiFile) {
    Copy-Item $msiFile.FullName $layout -Force
}

& (Join-Path $PSScriptRoot "pack-embedded-setup-payload.ps1") -LayoutDir $layout -MsiPath $msiPath -Version $Version
$layoutZip = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.Setup\Resources\installer-layout.zip"
$setupProj = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.Setup\SmartPerformanceDoctor.Setup.csproj"
if (-not (Test-Path $layoutZip)) {
    throw "Layout zip missing before publish: $layoutZip"
}
Write-Host "Publishing self-contained Setup (single-file host)..." -ForegroundColor Yellow
dotnet publish $setupProj -c Release -p:Platform=x64 -r win-x64 --self-contained true `
    -p:Version=$Version -p:FileVersion=$Version -p:AssemblyVersion=$Version `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) { throw "Setup publish failed." }
$publishCandidates = @(
    (Join-Path $setupOut "win-x64\publish\SmartPerformanceDoctor.Setup.exe"),
    (Join-Path $setupOut "publish\SmartPerformanceDoctor.Setup.exe"),
    (Join-Path $setupOut "SmartPerformanceDoctor.Setup.exe")
)
$baseSetup = $publishCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $baseSetup) { throw "Published Setup EXE not found under $setupOut" }
Write-Host "[OK] Setup host: $baseSetup ($([math]::Round((Get-Item $baseSetup).Length/1MB, 1)) MB)" -ForegroundColor Green
if (-not $SkipSigning) {
    & (Join-Path $PSScriptRoot "sign-modular-setup.ps1") -Version $Version -SkipIfNoCert -Targets @($baseSetup)
}
& (Join-Path $PSScriptRoot "append-setup-payload.ps1") -SetupExe $baseSetup -LayoutZip $layoutZip -MsiPath $msiPath -OutputExe $bundlePath
$setupExe = $bundlePath

function Get-Sha256([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    return (Get-FileHash $path -Algorithm SHA256).Hash.ToLower()
}

$artifacts = @(
    [PSCustomObject]@{ role = "setup-exe"; path = $bundlePath; sha256 = (Get-Sha256 $bundlePath) }
)

$manifest = [PSCustomObject]@{
    product = "Smart Performance Doctor"
    version = $Version
    buildType = "modular-installer"
    createdAt = (Get-Date).ToString("o")
    layoutPath = $layout
    setupBundle = $bundlePath
    msiEmbedded = $true
    msi = if ($msiFile) { $msiFile.FullName } else { $null }
    features = @{
        required = @("core-diagnostics", "report-audit", "program-integrity", "config-manager", "update-manifest")
        optional = @(
            "system-care", "driver-audio-repair", "secure-vault", "professional-secure-delete",
            "registry-doctor", "disk-doctor", "privacy-cleaner", "junk-cleaner", "shortcut-repair",
            "internet-acceleration", "vulnerability-fix", "deep-scan-intelligence", "knowledge-pack", "portable-tools"
        )
    }
    artifacts = $artifacts
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $installerDir "INSTALLER_MANIFEST.json") -Encoding UTF8

$sumLines = New-Object System.Collections.Generic.List[string]
foreach ($item in $artifacts) {
    if ($item.sha256) {
        $sumLines.Add("$($item.sha256)  $(Split-Path $item.path -Leaf)")
    }
}
$sumLines | Set-Content (Join-Path $installerDir "SHA256SUMS.txt") -Encoding UTF8

$updatePackageName = "PCCare_Update_v$Version.spdup"
$updatePackagePath = Join-Path $ProjectRoot "dist\updates\$updatePackageName"
$updateHash = Get-Sha256 $updatePackagePath

$channel = [PSCustomObject]@{
    product = "PC 케어 프로"
    channel = "stable"
    latestVersion = $Version
    minimumSupportedVersion = "45.0.0"
    createdAt = (Get-Date).ToString("o")
    releaseNotes = "모듈형 설치 관리자 — 기능별 선택 설치 및 installed_features.json 연동"
    artifacts = @{
        setup = @{
            file = $bundleName
            sha256 = (Get-Sha256 $bundlePath)
        }
        update = if ($updateHash) {
            @{
                file = $updatePackageName
                sha256 = $updateHash
            }
        } else {
            $null
        }
        msi = $null
    }
    safety = @{
        requiresManualInstall = $true
        checksumRequired = $true
        signatureRecommended = $true
    }
}
$channel | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $installerDir "UPDATE_CHANNEL.json") -Encoding UTF8

$readmePath = Join-Path $installerDir "INSTALLER_README.md"
@'
# PC 케어 프로 v{0} — 단일 설치 파일

Built: {1}

## 배포 산출물
- {2} — 설치 마법사 + 런타임 + MSI 페이로드가 하나로 포함된 단일 설치 파일

## 사용 방법
1. {2} 를 관리자 권한으로 실행 (권장)
2. 기능 선택 후 설치 — installed_features.json 이 ProgramData 에 기록됩니다
3. 이후 변경: 앱 > 기능 관리 > 설치 관리자

## 빌드
  .\scripts\build-modular-setup.ps1 -Version {0}
'@ -f $Version, (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $bundleName |
    Set-Content $readmePath -Encoding UTF8

Write-Host "[OK] Setup bundle: $bundlePath" -ForegroundColor Green
Write-Host "[OK] Layout: $layout" -ForegroundColor Green
Write-Host "[OK] Manifest: $(Join-Path $installerDir 'INSTALLER_MANIFEST.json')" -ForegroundColor Green
