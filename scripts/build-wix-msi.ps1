param(
    [string]$SourceDir = "",
    [string]$Version = "45.0.7",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

if (-not $SourceDir) { $SourceDir = Join-Path $ProjectRoot "artifacts\installer\layout" }
if (-not $OutputDir) { $OutputDir = Join-Path $ProjectRoot "artifacts\installer" }

Write-Host "== Build WiX MSI (v$Version) ==" -ForegroundColor Cyan

if (-not (Test-Path $SourceDir)) {
    throw "소스 폴더가 없습니다. 먼저 .\scripts\publish-consumer.ps1을 실행하세요: $SourceDir"
}

function Find-Wix {
    $cmd = Get-Command wix -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $toolWix = Join-Path $env:USERPROFILE ".dotnet\tools\wix.exe"
    if (Test-Path $toolWix) { return $toolWix }

    return $null
}

function Escape-WixPath([string]$path) {
    return $path.Replace('\', '\\')
}

function New-DirectoryId([string]$relative) {
    if ([string]::IsNullOrWhiteSpace($relative)) { return "INSTALLFOLDER" }
    return "DIR_" + ($relative -replace '[^A-Za-z0-9]', '_')
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$layout = Join-Path $OutputDir "layout"
$sourceResolved = (Resolve-Path $SourceDir).Path
$layoutResolvedPath = Resolve-Path $layout -ErrorAction SilentlyContinue
$layoutResolved = if ($layoutResolvedPath) { $layoutResolvedPath.Path } else { $null }

if ($sourceResolved -ne $layoutResolved) {
    if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
    Copy-Item $SourceDir $layout -Recurse -Force
} elseif (-not (Test-Path $layout)) {
    throw "설치 레이아웃이 없습니다. 먼저 .\scripts\prepare-installer-layout.ps1을 실행하세요."
}

$componentsPath = Join-Path $OutputDir "Components.wxs"
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <ComponentGroup Id="AppFiles" Directory="INSTALLFOLDER">')

$dirIds = @{ "" = "INSTALLFOLDER" }
Get-ChildItem $layout -Recurse -Directory | ForEach-Object {
    $relative = $_.FullName.Substring($layout.Length + 1)
    $dirIds[$relative] = New-DirectoryId $relative
}

$id = 0
Get-ChildItem $layout -Recurse -File | ForEach-Object {
    $id++
    $relative = $_.FullName.Substring($layout.Length + 1)
    $parentRelative = Split-Path $relative -Parent
    $dirId = $dirIds[[string]$parentRelative]
    $compId = "CMP_$id"
    $source = Escape-WixPath $_.FullName

    [void]$sb.AppendLine("      <Component Id=`"$compId`" Directory=`"$dirId`" Guid=`"*`">")
    [void]$sb.AppendLine("        <File Id=`"FILE_$id`" Source=`"$source`" KeyPath=`"yes`" />")
    [void]$sb.AppendLine('      </Component>')
}

[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <DirectoryRef Id="INSTALLFOLDER">')

function Append-DirectoryTree([string]$parentKey, [int]$indent) {
    $pad = ' ' * $indent
    $children = $dirIds.Keys | Where-Object { $_ -ne '' -and (Split-Path $_ -Parent) -eq $parentKey } | Sort-Object
    foreach ($childKey in $children) {
        $dirId = $dirIds[$childKey]
        $name = Split-Path $childKey -Leaf
        $grandChildren = $dirIds.Keys | Where-Object { $_ -ne '' -and (Split-Path $_ -Parent) -eq $childKey }
        if ($grandChildren.Count -gt 0) {
            [void]$sb.AppendLine("$pad<Directory Id=`"$dirId`" Name=`"$name`">")
            Append-DirectoryTree $childKey ($indent + 2)
            [void]$sb.AppendLine("$pad</Directory>")
        } else {
            [void]$sb.AppendLine("$pad<Directory Id=`"$dirId`" Name=`"$name`" />")
        }
    }
}
Append-DirectoryTree '' 6

[void]$sb.AppendLine('    </DirectoryRef>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')
Set-Content $componentsPath $sb.ToString() -Encoding UTF8

$markersDir = Join-Path $OutputDir "feature-markers"
if (Test-Path $markersDir) { Remove-Item $markersDir -Recurse -Force }
New-Item -ItemType Directory -Path $markersDir -Force | Out-Null
@(
    "system-care", "driver-audio-repair", "secure-vault", "professional-secure-delete",
    "registry-doctor", "disk-doctor", "privacy-cleaner", "junk-cleaner", "shortcut-repair",
    "internet-acceleration", "vulnerability-fix", "deep-scan-intelligence", "knowledge-pack", "portable-tools"
) | ForEach-Object {
    Set-Content (Join-Path $markersDir "$_.marker") "feature=$_" -Encoding UTF8
}

$msiName = "SmartPerformanceDoctor_v$($Version -replace '\.','_').msi"
$msiPath = Join-Path $OutputDir $msiName
$wix = Find-Wix

if ($wix) {
    $wixDir = Join-Path $ProjectRoot "artifacts\installer\templates\wix"
    if (-not (Test-Path (Join-Path $wixDir "Product.wxs"))) {
        $wixDir = Join-Path $ProjectRoot "installer\wix"
    }
    $productWxs = Join-Path $wixDir "Product.wxs"
    $markersWxs = Join-Path $wixDir "FeatureMarkers.wxs"
    $markersDirEscaped = $markersDir.Replace('\', '\\')
    & $wix build -acceptEula wix7 $productWxs $componentsPath $markersWxs -d ProductVersion=$Version -d FeatureMarkersDir=$markersDirEscaped -o $msiPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $msiPath)) {
        Write-Host "[PENDING] WiX 빌드 실패 — WiX v7은 OSMF EULA 동의가 필요합니다." -ForegroundColor Yellow
        Write-Host "          https://wixtoolset.org/osmf/ 참고 후 wix build를 다시 실행하세요." -ForegroundColor Yellow
        Write-Host "[OK] Components draft: $componentsPath" -ForegroundColor Green
        return
    }
    Write-Host "[OK] MSI: $msiPath" -ForegroundColor Green
} else {
    Write-Host "[INFO] WiX Toolset이 설치되어 있지 않습니다." -ForegroundColor Yellow
    Write-Host "       설치: dotnet tool install --global wix" -ForegroundColor Yellow
    Write-Host "[OK] Installer layout: $layout" -ForegroundColor Green
    Write-Host "[OK] Components draft: $componentsPath" -ForegroundColor Green
    Write-Host "[PENDING] WiX 설치 후 동일 스크립트를 다시 실행하면 MSI가 생성됩니다." -ForegroundColor Yellow
}