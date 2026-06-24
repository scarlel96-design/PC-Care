param(
    [string]$SourceDir = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
if (-not $SourceDir) {
    $runtimeRoot = Get-RuntimeRoot -ProjectRoot $ProjectRoot
    $SourceDir = if (Test-RuntimePublished $runtimeRoot) { $runtimeRoot } else {
        Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0"
    }
}

$runtimeDirs = @("engine", "content", "runtimes", "Views", "Controls", "Resources", "Styles")
$runtimeExtensions = @(".exe", ".dll", ".json", ".pri", ".xbf", ".bat", ".txt", ".ico")

Write-Host "== Prepare installer layout ==" -ForegroundColor Cyan
Write-Host "Source: $SourceDir"

$layout = Join-Path $ProjectRoot "artifacts\installer\layout"
if (Test-Path $layout) {
    Remove-Item $layout -Recurse -Force
}
New-Item -ItemType Directory -Path $layout -Force | Out-Null

Get-ChildItem $SourceDir -File | Where-Object {
    $runtimeExtensions -contains $_.Extension.ToLowerInvariant()
} | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $layout $_.Name) -Force
}

foreach ($dirName in $runtimeDirs) {
    $src = Join-Path $SourceDir $dirName
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $layout $dirName) -Recurse -Force
    }
}

foreach ($extra in @("README.txt")) {
    $src = Join-Path $SourceDir $extra
    if (Test-Path $src) {
        Copy-Item $src $layout -Force
    }
}

@(
    "PC 케어 프로 installer layout",
    "",
    "Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
    "Source: $SourceDir",
    "",
    "Layout:",
    "  PCCare.exe (실행) / SmartPerformanceDoctor.exe (런타임 호스트)",
    "  engine/ - core, repair, Aegis",
    "  content/ - rules, assets, data",
    "  runtimes/, Views/, Controls/ ..."
) | Set-Content (Join-Path $layout "INSTALLER_README.txt") -Encoding UTF8

Write-Host "[OK] Installer layout: $layout" -ForegroundColor Green