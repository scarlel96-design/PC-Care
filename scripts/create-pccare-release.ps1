param(
    [string]$Version = "50.1.1",
    [string]$ReleaseRoot = "",
    [string]$ReleaseNotes = ""
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$CodingRoot = (Resolve-Path (Join-Path $ProjectRoot "..\..")).Path

if (-not $ReleaseRoot) {
    $ReleaseRoot = Join-Path $CodingRoot "PCCare_Release_v$Version"
}
New-Item -ItemType Directory -Path $ReleaseRoot -Force | Out-Null

$setupSrc = Join-Path $ProjectRoot "artifacts\installer\setup\PCCare_Setup_v$Version.exe"
$updateSrc = Join-Path $ReleaseRoot "PCCare_Update_v$Version.spdup"
if (-not (Test-Path $updateSrc)) {
    $updateSrc = Join-Path $ProjectRoot "dist\updates\PCCare_Update_v$Version.spdup"
}

if (-not (Test-Path $setupSrc)) {
    throw "설치 파일이 없습니다. build-modular-setup.ps1 를 먼저 실행하세요: $setupSrc"
}
if (-not (Test-Path $updateSrc)) {
    throw "업데이트 파일이 없습니다. create-update-package.ps1 를 먼저 실행하세요."
}

$setupDest = Join-Path $ReleaseRoot "PCCare_Setup_v$Version.exe"
$updateDest = Join-Path $ReleaseRoot "PCCare_Update_v$Version.spdup"
if (-not $setupSrc.Equals($setupDest, [StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item $setupSrc $setupDest -Force
}
if (-not $updateSrc.Equals($updateDest, [StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item $updateSrc $updateDest -Force
}

$setupHash = (Get-FileHash $setupDest -Algorithm SHA256).Hash.ToLowerInvariant()
$updateHash = (Get-FileHash $updateDest -Algorithm SHA256).Hash.ToLowerInvariant()

if (-not $ReleaseNotes) {
    $changelog = Join-Path $ProjectRoot "updates\CHANGELOG_v$Version.json"
    if (Test-Path $changelog) {
        $doc = Get-Content $changelog -Raw -Encoding UTF8 | ConvertFrom-Json
        $lines = @("# PC 케어 프로 v$Version", "", "## 변경 사항")
        if ($doc.changes) {
            foreach ($change in @($doc.changes)) {
                $lines += "- $change"
            }
        }
        else {
            $lines += "- $([string]$doc.releaseNotes)"
        }
        $ReleaseNotes = ($lines -join "`n")
    }
    else {
        $ReleaseNotes = @"
# PC 케어 프로 v$Version

## 변경 사항
- 설치 폴더 구조 상업용 정리

update-sha256: $updateHash
"@
    }
}

$githubBody = @"
$ReleaseNotes

---
**배포 파일**
- 설치: ``PCCare_Setup_v$Version.exe``
- 업데이트: ``PCCare_Update_v$Version.spdup``

``update-sha256: $updateHash``
"@

$manifest = [PSCustomObject]@{
    product = "PC 케어 프로"
    version = $Version
    repository = "https://github.com/scarlel96-design/PC-Care"
    createdAt = (Get-Date).ToString("o")
    artifacts = [PSCustomObject]@{
        setup = [PSCustomObject]@{ file = "PCCare_Setup_v$Version.exe"; sha256 = $setupHash }
        update = [PSCustomObject]@{ file = "PCCare_Update_v$Version.spdup"; sha256 = $updateHash }
    }
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $ReleaseRoot "release-manifest.json") -Encoding UTF8
$manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $ProjectRoot "release-manifest.json") -Encoding UTF8
$githubBody | Set-Content (Join-Path $ReleaseRoot "GITHUB_RELEASE.md") -Encoding UTF8
$channelPath = Join-Path $ReleaseRoot "UPDATE_CHANNEL.json"
$channel = [PSCustomObject]@{
    product = "PC 케어 프로"
    channel = "stable"
    latestVersion = $Version
    minimumSupportedVersion = "45.0.0"
    createdAt = (Get-Date).ToString("o")
    releaseNotes = ($ReleaseNotes -split "`n" | Select-Object -First 6) -join "`n"
    artifacts = [PSCustomObject]@{
        setup = [PSCustomObject]@{ file = "PCCare_Setup_v$Version.exe"; sha256 = $setupHash }
        update = [PSCustomObject]@{ file = "PCCare_Update_v$Version.spdup"; sha256 = $updateHash }
        msi = $null
    }
    safety = [PSCustomObject]@{
        requiresManualInstall = $true
        checksumRequired = $true
        signatureRecommended = $true
    }
}
$channel | ConvertTo-Json -Depth 8 | Set-Content $channelPath -Encoding UTF8
$channel | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $ProjectRoot "UPDATE_CHANNEL.json") -Encoding UTF8
@(
    "$setupHash  PCCare_Setup_v$Version.exe",
    "$updateHash  PCCare_Update_v$Version.spdup"
) | Set-Content (Join-Path $ReleaseRoot "SHA256SUMS.txt") -Encoding UTF8

$githubRoot = Join-Path $ReleaseRoot "github-repo-root"
New-Item -ItemType Directory -Path $githubRoot -Force | Out-Null
Copy-Item (Join-Path $ReleaseRoot "release-manifest.json") (Join-Path $githubRoot "release-manifest.json") -Force
Copy-Item (Join-Path $ReleaseRoot "UPDATE_CHANNEL.json") (Join-Path $githubRoot "UPDATE_CHANNEL.json") -Force
@"
# PC-Care 저장소(main)에 올릴 메타데이터

앱은 다음 순서로 원격 업데이트를 조회합니다.
1. ``https://raw.githubusercontent.com/scarlel96-design/PC-Care/main/release-manifest.json``
2. ``.../main/UPDATE_CHANNEL.json``
3. GitHub Releases API (prerelease 포함 목록 — ``/releases/latest`` 만으로는 404 가능)
4. 릴리즈 태그에 첨부한 ``release-manifest.json`` / ``UPDATE_CHANNEL.json``

## 권장
- GitHub Releases에 ``PCCare_Update_v$Version.spdup`` 첨부 (태그 ``v$Version``)
- 이 폴더의 두 JSON을 **PC-Care 저장소 main 루트**에 커밋·푸시
- 또는 동일 파일을 릴리즈 자산으로도 업로드
"@ | Set-Content (Join-Path $githubRoot "README_GITHUB_SYNC.md") -Encoding UTF8

Write-Host "[OK] Release folder: $ReleaseRoot" -ForegroundColor Green
Write-Host "[OK] Setup:  PCCare_Setup_v$Version.exe" -ForegroundColor Green
Write-Host "[OK] Update: PCCare_Update_v$Version.spdup" -ForegroundColor Green
Write-Host "[OK] GitHub body: GITHUB_RELEASE.md" -ForegroundColor Green
Write-Host "[OK] GitHub main sync: $githubRoot" -ForegroundColor Green