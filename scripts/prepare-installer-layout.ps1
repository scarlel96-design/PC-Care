param(
    [string]$SourceDir = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
if (-not $SourceDir) {
    $runtimeRoot = Get-RuntimeRoot -ProjectRoot $ProjectRoot
    $publishOut = Get-AppPublishOutput -ProjectRoot $ProjectRoot
    if (Test-SelfContainedRuntimeLayout $publishOut) {
        $SourceDir = $publishOut
    }
    elseif (Test-SelfContainedRuntimeLayout $runtimeRoot) {
        $SourceDir = $runtimeRoot
    }
    elseif (Test-RuntimePublished $runtimeRoot) {
        $SourceDir = $runtimeRoot
    }
    else {
        $SourceDir = $publishOut
    }
}

$sourceResolved = (Resolve-Path -LiteralPath $SourceDir -ErrorAction Stop).Path

Write-Host "== Prepare installer layout ==" -ForegroundColor Cyan
Write-Host "Source: $sourceResolved"

$layout = Join-Path $ProjectRoot "artifacts\installer\layout"
if (Test-Path $layout) {
    Remove-Item $layout -Recurse -Force
}
New-Item -ItemType Directory -Path $layout -Force | Out-Null

$robocopy = Get-Command robocopy -ErrorAction SilentlyContinue
if ($robocopy) {
    $null = & robocopy $sourceResolved $layout /E /NFL /NDL /NJH /NJS /NC /NS /NP /XD obj src scripts tests artifacts target dist updates .git .vscode /XF *.pdb /R:1 /W:1 2>&1
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed (exit $LASTEXITCODE): $sourceResolved -> $layout"
    }
}
else {
    Get-ChildItem $sourceResolved -Recurse -File | Where-Object { $_.Extension -ne '.pdb' } | ForEach-Object {
        $relative = $_.FullName.Substring($sourceResolved.Length + 1)
        $dest = Join-Path $layout $relative
        $destDir = Split-Path $dest -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        Copy-Item $_.FullName $dest -Force
    }
}

function Write-PccareBranding {
    param([string]$Root)
    $mainExe = Join-Path $Root "SmartPerformanceDoctor.exe"
    if (-not (Test-Path $mainExe)) {
        throw "SmartPerformanceDoctor.exe missing in layout source: $Root"
    }
    Copy-Item $mainExe (Join-Path $Root "PCCare.exe") -Force
    foreach ($pair in @(
            @{ Src = "SmartPerformanceDoctor.deps.json"; Dest = "PCCare.deps.json" },
            @{ Src = "SmartPerformanceDoctor.runtimeconfig.json"; Dest = "PCCare.runtimeconfig.json" },
            @{ Src = "SmartPerformanceDoctor.pri"; Dest = "PCCare.pri" }
        )) {
        $srcPath = Join-Path $Root $pair.Src
        if (Test-Path $srcPath) {
            Copy-Item $srcPath (Join-Path $Root $pair.Dest) -Force
        }
    }
}

Write-PccareBranding -Root $layout
Sync-InstallLayoutUiAssets -LayoutDir $layout -ProjectRoot $ProjectRoot

function Sync-WinUiPriAlias {
    param([string]$Root)
    $mapPri = Join-Path $Root "Microsoft.UI.Xaml.pri"
    if (Test-Path $mapPri) { return }
    $controlsPri = Join-Path $Root "Microsoft.UI.Xaml.Controls.pri"
    if (-not (Test-Path $controlsPri)) {
        throw "WinUI PRI missing in layout: Microsoft.UI.Xaml.Controls.pri"
    }
    Copy-Item $controlsPri $mapPri -Force
    Write-Host "[OK] WinUI PRI alias: Microsoft.UI.Xaml.pri" -ForegroundColor DarkGray
}

Sync-WinUiPriAlias -Root $layout
Sync-WinUiThemeAssets -TargetDir $layout -ProjectRoot $ProjectRoot

& (Join-Path $PSScriptRoot "sanitize-commercial-layout.ps1") -LayoutDir $layout
& (Join-Path $PSScriptRoot "verify-install-layout.ps1") -LayoutDir $layout

Write-Host "[OK] Installer layout: $layout" -ForegroundColor Green