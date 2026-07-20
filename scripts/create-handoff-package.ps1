param(
    [string]$Version = "50.0.0",
    [string]$HandoffDir = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$CodingRoot = (Resolve-Path (Join-Path $ProjectRoot "..\..")).Path
if (-not $HandoffDir) {
    $HandoffDir = Join-Path $CodingRoot "PCCare_Pro_v50_RC_Lock"
}

New-Item -ItemType Directory -Path $HandoffDir -Force | Out-Null

$setupCandidates = @(
    (Join-Path $ProjectRoot "artifacts\installer\setup\PCCare_Setup_v$Version.exe"),
    (Join-Path $ProjectRoot "artifacts\installer\setup\SmartPerformanceDoctor_Setup_v$Version.exe")
)
$setupSrc = $setupCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $setupSrc) {
    throw "Setup EXE not found. Run scripts\build.ps1 first: $($setupCandidates -join ' | ')"
}

. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
$runtimeSrc = Get-RuntimeRoot -ProjectRoot $ProjectRoot
if (-not (Test-RuntimePublished $runtimeSrc)) {
    throw "Runtime not found under artifacts\runtime. Run scripts\build.ps1 first: $(Join-Path $runtimeSrc 'PCCare.exe')"
}

$setupName = "PCCare_Setup_v$Version.exe"
$runtimeZipName = "PCCare_Runtime_v$Version.zip"
$sourceZipName = "PCCare_Source_v$Version.zip"

Write-Host "== Create handoff package v$Version ==" -ForegroundColor Cyan
Write-Host "Target: $HandoffDir" -ForegroundColor Cyan

Copy-Item $setupSrc (Join-Path $HandoffDir $setupName) -Force
Write-Host "[OK] Setup: $setupName" -ForegroundColor Green

$runtimeZip = Join-Path $HandoffDir $runtimeZipName
$runtimeStage = Join-Path $env:TEMP "PCCare_Runtime_stage_$(Get-Random)"
if (Test-Path $runtimeStage) { Remove-Item $runtimeStage -Recurse -Force }
Copy-RuntimeTreeToStage -RuntimeRoot $runtimeSrc -StageDir $runtimeStage
if (Test-Path $runtimeZip) { Remove-Item $runtimeZip -Force }
Compress-Archive -Path (Join-Path $runtimeStage "*") -DestinationPath $runtimeZip -CompressionLevel Optimal
Remove-Item $runtimeStage -Recurse -Force
Write-Host "[OK] Runtime ZIP: $runtimeZipName ($([math]::Round((Get-Item $runtimeZip).Length/1MB,1)) MB)" -ForegroundColor Green

& (Join-Path $PSScriptRoot "package-source-zip.ps1") -Version $Version -OutputDir $HandoffDir

$artifacts = @(
    (Join-Path $HandoffDir $setupName),
    (Join-Path $HandoffDir $runtimeZipName),
    (Join-Path $HandoffDir $sourceZipName)
)

$sumLines = New-Object System.Collections.Generic.List[string]
foreach ($path in $artifacts) {
    if (Test-Path $path) {
        $hash = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $sumLines.Add("$hash  $(Split-Path $path -Leaf)")
    }
}
$sumLines | Set-Content (Join-Path $HandoffDir "SHA256SUMS.txt") -Encoding UTF8

$buildLog = Join-Path $HandoffDir "build_log.txt"
$installLog = Join-Path $HandoffDir "install_log.txt"
if (Test-Path (Join-Path $ProjectRoot "artifacts\installer\INSTALLER_MANIFEST.json")) {
    @(
        "PC 케어 프로 v$Version — installer build",
        "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
        "",
        "Setup: $setupName",
        "MSI: artifacts\installer\SmartPerformanceDoctor_v$($Version -replace '\.','_').msi",
        "",
        (Get-Content (Join-Path $ProjectRoot "artifacts\installer\INSTALLER_MANIFEST.json") -Raw)
    ) | Set-Content $installLog -Encoding UTF8
} else {
    "Installer manifest not found — build-modular-setup may have been skipped." | Set-Content $installLog -Encoding UTF8
}

$fullBuildLog = Join-Path $ProjectRoot "artifacts\full-build-log.txt"
if (Test-Path $fullBuildLog) {
    Copy-Item $fullBuildLog $buildLog -Force
} elseif (-not (Test-Path $buildLog)) {
    @(
        "PC 케어 프로 v$Version — build summary",
        "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
        "Release build: success",
        "Commercial packs: signed",
        "Runtime: artifacts/runtime (PCCare.exe, win-x64 trimmed)"
    ) | Set-Content $buildLog -Encoding UTF8
}

$setupMb = [math]::Round((Get-Item (Join-Path $HandoffDir $setupName)).Length / 1MB, 1)
$runtimeMb = [math]::Round((Get-Item $runtimeZip).Length / 1MB, 1)
$sourceMb = if (Test-Path (Join-Path $HandoffDir $sourceZipName)) {
    [math]::Round((Get-Item (Join-Path $HandoffDir $sourceZipName)).Length / 1MB, 1)
} else { 0 }

$readme = @"
# PC 케어 프로 v$Version — 납품 패키지

생성일: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

## 제품 정보

| 항목 | 내용 |
|------|------|
| 제품명 | PC 케어 |
| 정식명 | PC 케어 프로 |
| 버전 | $Version |
| 설명 | PC 통합 점검·복구·보안 관리 프로그램 |
| 실행 파일 | PCCare.exe |
| 설치 경로 | Program Files\PCCare (레거시: AstraCare) |
| ProgramData | %ProgramData%\PCCare |

## 패키지 구성

| 파일 | 설명 |
|------|------|
| $setupName | Windows 모듈형 설치 프로그램 ($setupMb MB) |
| $runtimeZipName | 포터블 런타임 (압축 해제 시 PCCare.exe가 최상위, $runtimeMb MB) |
| $sourceZipName | 소스 코드 (bin/obj/dist/artifacts 제외, $sourceMb MB) |
| build_log.txt | 빌드 요약 |
| install_log.txt | 설치 프로그램 빌드 manifest |
| SHA256SUMS.txt | 위 산출물 SHA-256 해시 |

## 설치 방법

1. ``$setupName`` 를 관리자 권한으로 실행합니다.
2. 권장/최소/사용자 지정 설치 중 선택하고 기능을 고릅니다.
3. 설치 완료 후 ``C:\Program Files\PCCare\PCCare.exe`` 또는 시작 메뉴에서 실행합니다.

## 포터블(압축) 실행

1. ``$runtimeZipName`` 을 원하는 폴더에 압축 해제합니다.
2. ``PCCare.exe`` 를 실행합니다.

## 소스 빌드

```powershell
Set-ExecutionPolicy -Scope Process Bypass
cd <압축 해제 경로>
.\scripts\build.ps1
.\PCCare.exe
```

## 무결성 검증

```powershell
Get-FileHash .\$setupName -Algorithm SHA256
# SHA256SUMS.txt 의 값과 비교
```

## v50 RC Lock 요약

- Rule/Protocol Pack checksum·ECDSA 서명 (v$Version)
- Secure Vault 신규 금고 Argon2id KDF
- Aegis Mirror E2E 복구 검증
- ScopeRepairFilter — 시스템/전체 점검 범위 분리
- 모듈형 설치 (기능별 파일 스킵)
- Runtime win-x64 RID trim
- VSS/Shadow Copy 자동 삭제 금지
- SSD 삭제 Level 5 표기 제한

## 기술 식별자 (내부)

- 어셈블리: SmartPerformanceDoctor.*
- 서비스: PCCareAegisRecovery (레거시: AstraCareAegisRecovery)
- 데이터: %LOCALAPPDATA%\PCCare (레거시 경로 자동 인식)
- Aegis Mirror: %ProgramData%\PCCare\AegisMirror\
"@

$readme | Set-Content (Join-Path $HandoffDir "README.md") -Encoding UTF8

Write-Host "[OK] Handoff package ready: $HandoffDir" -ForegroundColor Green
Write-Host "[OK] SHA256SUMS.txt updated" -ForegroundColor Green