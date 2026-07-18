param(
    [Parameter(Mandatory = $true)]
    [string]$LayoutDir
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")

$layoutResolved = (Resolve-Path -LiteralPath $LayoutDir -ErrorAction Stop).Path
if (Test-IsUnsafeSanitizeTarget -TargetDir $layoutResolved -ProjectRoot $ProjectRoot) {
    throw "sanitize-commercial-layout.ps1 은 설치 스테이징 경로에만 실행할 수 있습니다. 프로젝트 루트/소스 트리는 차단됩니다: $layoutResolved"
}

Write-Host "== Sanitize commercial install layout ==" -ForegroundColor Cyan
Write-Host "Target: $layoutResolved"

$blockedRootFiles = Get-CommercialBlockedRootFileNames
foreach ($name in $blockedRootFiles) {
    $path = Join-Path $layoutResolved $name
    if (Test-Path $path) {
        Remove-Item $path -Force -ErrorAction SilentlyContinue
        Write-Host "[CLEAN] $name" -ForegroundColor DarkGray
    }
}

Get-ChildItem $layoutResolved -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in @('.msi', '.pdb', '.wixpdb', '.wixobj') } |
    ForEach-Object {
        Remove-Item $_.FullName -Force
        $rel = $_.FullName.Substring($layoutResolved.Length + 1)
        Write-Host "[CLEAN] $rel" -ForegroundColor DarkGray
    }

foreach ($pattern in @("SmartPerformanceDoctor.Setup*", "AstraCare_Setup*")) {
    Get-ChildItem $layoutResolved -Filter $pattern -File -Recurse -ErrorAction SilentlyContinue |
        ForEach-Object {
            Remove-Item $_.FullName -Force
            $rel = $_.FullName.Substring($layoutResolved.Length + 1)
            Write-Host "[CLEAN] $rel" -ForegroundColor DarkGray
        }
}

foreach ($dirName in Get-CommercialBlockedDirectoryNames) {
    $path = Join-Path $layoutResolved $dirName
    if (Test-Path $path) {
        Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "[CLEAN] $dirName\" -ForegroundColor DarkGray
    }
}

$engineDir = Join-Path $layoutResolved "engine"
if (Test-Path $engineDir) {
    foreach ($alias in Get-CommercialBlockedEngineAliases) {
        $path = Join-Path $engineDir $alias
        if (Test-Path $path) {
            Remove-Item $path -Force
            Write-Host "[CLEAN] engine\$alias" -ForegroundColor DarkGray
        }
    }
}

$rulesDir = Join-Path $layoutResolved "content\rules"
if (Test-Path $rulesDir) {
    $ruleFiles = Get-ChildItem $rulesDir -File -ErrorAction SilentlyContinue
    if (-not $ruleFiles -or $ruleFiles.Count -eq 0) {
        Remove-Item $rulesDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "[CLEAN] empty content\rules\" -ForegroundColor DarkGray
    }
}

$dataRulesDir = Join-Path $layoutResolved "data\rules"
if (Test-Path $dataRulesDir) {
    $ruleFiles = Get-ChildItem $dataRulesDir -File -ErrorAction SilentlyContinue
    if (-not $ruleFiles -or $ruleFiles.Count -eq 0) {
        Remove-Item (Join-Path $layoutResolved "data") -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "[CLEAN] empty data\" -ForegroundColor DarkGray
    }
}

Get-CommercialFolderLayoutText | Set-Content (Join-Path $layoutResolved "FOLDER_LAYOUT.txt") -Encoding UTF8

$readmePath = Join-Path $layoutResolved "README.txt"
if (-not (Test-Path $readmePath)) {
    @(
        "PC 케어 프로",
        "",
        "Run: PCCare.exe",
        "",
        "See FOLDER_LAYOUT.txt for install folder structure."
    ) | Set-Content $readmePath -Encoding UTF8
}

$violations = @(Get-InstallLayoutViolations -LayoutDir $layoutResolved)
if ($violations.Count -gt 0) {
    throw "상업용 설치 레이아웃 검증 실패:`n$($violations -join "`n")"
}

Write-Host "[OK] Commercial layout sanitized." -ForegroundColor Green