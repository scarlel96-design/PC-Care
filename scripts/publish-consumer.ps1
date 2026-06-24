param(
    [string]$AppOut = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

Write-Host "== Smart Performance Doctor v44 Consumer Publish ==" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "build.ps1")

$version = "44.0.0"
$runtimeRoot = $ProjectRoot
$manifestPath = Join-Path $ProjectRoot ".consumer-publish-manifest.json"

if (-not $AppOut) {
    $AppOut = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0"
}

if (-not (Test-Path $AppOut)) {
    throw "앱 출력 폴더를 찾지 못했습니다: $AppOut"
}

function Remove-PublishedRuntime {
    if (-not (Test-Path $manifestPath)) { return }

    $previous = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($relative in $previous.files) {
        $path = Join-Path $ProjectRoot ($relative -replace '/', '\')
        if (Test-Path $path) {
            Remove-Item $path -Force -ErrorAction SilentlyContinue
        }
    }
    if ($previous.directories) {
        foreach ($dir in $previous.directories) {
            $path = Join-Path $ProjectRoot ($dir -replace '/', '\')
            if (Test-Path $path) {
                Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
    Remove-Item $manifestPath -Force -ErrorAction SilentlyContinue
}

Remove-PublishedRuntime

$publishedFiles = New-Object System.Collections.Generic.List[string]
$publishedDirs = New-Object System.Collections.Generic.HashSet[string]

Get-ChildItem $AppOut -Recurse -File | Where-Object { $_.Extension -ne ".pdb" } | ForEach-Object {
    $relative = $_.FullName.Substring($AppOut.Length + 1).Replace('\', '/')
    $dest = Join-Path $runtimeRoot ($relative -replace '/', '\')
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    Copy-Item $_.FullName $dest -Force
    $publishedFiles.Add($relative) | Out-Null

    $dirRelative = Split-Path $relative -Parent
    while ($dirRelative) {
        $publishedDirs.Add(($dirRelative -replace '\\', '/')) | Out-Null
        $dirRelative = Split-Path $dirRelative -Parent
    }
}

$bootstrap = Join-Path $runtimeRoot "runtimes\win-x64\native\Microsoft.WindowsAppRuntime.Bootstrap.dll"
if (-not (Test-Path $bootstrap)) {
    throw "필수 런타임 파일이 누락되었습니다: runtimes\win-x64\native\Microsoft.WindowsAppRuntime.Bootstrap.dll"
}

@{
    version = $version
    publishedAt = (Get-Date).ToString("o")
    files = $publishedFiles
    directories = @($publishedDirs | Sort-Object)
} | ConvertTo-Json -Depth 4 | Set-Content $manifestPath -Encoding UTF8

$readme = @(
    "스마트 성능 닥터 v$version",
    "",
    "실행: SmartPerformanceDoctor.exe 또는 시작.bat",
    "",
    "포함 구성:",
    "- SmartPerformanceDoctor.exe (메인 프로그램 — 루트)",
    "- engine\smart_performance_doctor_core.exe (진단 엔진)",
    "- engine\smart_performance_doctor_repair_helper.exe (복구 도우미)",
    "- content\rules, content\assets (진단 규칙·리소스)",
    "- rules/ (진단·복구 규칙)",
    "- assets/ (아이콘·디자인 리소스)",
    "- runtimes/ (Windows App SDK 네이티브 런타임)",
    "",
    "데이터 저장 위치:",
    "%LOCALAPPDATA%\SmartPerformanceDoctor\data\knowledge.db"
) -join "`r`n"

Set-Content (Join-Path $runtimeRoot "README.txt") $readme -Encoding UTF8

@(
    "@echo off",
    "cd /d `"%~dp0`"",
    "start `"`" `"%~dp0SmartPerformanceDoctor.exe`""
) -join "`r`n" | Set-Content (Join-Path $ProjectRoot "시작.bat") -Encoding ASCII

$distDir = Join-Path $ProjectRoot "dist"
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
$zip = Join-Path $distDir "SmartPerformanceDoctor_v44_Portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

$zipStage = Join-Path $distDir "_zip_stage"
if (Test-Path $zipStage) { Remove-Item $zipStage -Recurse -Force }
New-Item -ItemType Directory -Path $zipStage -Force | Out-Null

foreach ($relative in $publishedFiles) {
    $src = Join-Path $runtimeRoot ($relative -replace '/', '\')
    $dest = Join-Path $zipStage ($relative -replace '/', '\')
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    Copy-Item $src $dest -Force
}
Copy-Item (Join-Path $runtimeRoot "README.txt") $zipStage -Force
Copy-Item (Join-Path $runtimeRoot "시작.bat") $zipStage -Force

Compress-Archive -Path "$zipStage\*" -DestinationPath $zip -Force
Remove-Item $zipStage -Recurse -Force

Write-Host "[OK] 실행 파일: $(Join-Path $runtimeRoot 'SmartPerformanceDoctor.exe')" -ForegroundColor Green
Write-Host "[OK] 시작.bat: $(Join-Path $ProjectRoot '시작.bat')" -ForegroundColor Green
Write-Host "[OK] Portable zip: $zip" -ForegroundColor Green

& (Join-Path $PSScriptRoot "prepare-installer-layout.ps1") -SourceDir $runtimeRoot
& (Join-Path $PSScriptRoot "build-wix-msi.ps1") -Version $version
& (Join-Path $PSScriptRoot "build-msix.ps1") -Version "$version.0"
& (Join-Path $PSScriptRoot "sign-consumer.ps1") -SkipIfNoCert