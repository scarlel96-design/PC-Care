param(
    [string]$SourceDir = "",
    [string]$Version = "44.0.0.0",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

if (-not $SourceDir) { $SourceDir = Join-Path $ProjectRoot "artifacts\installer\layout" }
if (-not $OutputDir) { $OutputDir = Join-Path $ProjectRoot "artifacts\installer" }

Write-Host "== Build MSIX package (v$Version) ==" -ForegroundColor Cyan

if (-not (Test-Path $SourceDir)) {
    throw "소스 폴더가 없습니다. 먼저 .\scripts\publish-consumer.ps1을 실행하세요: $SourceDir"
}

function Find-MakeAppx {
    $cmd = Get-Command makeappx -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $kits) {
        $found = Get-ChildItem $kits -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    return $null
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$msixLayout = Join-Path $OutputDir "msix-layout"
if (Test-Path $msixLayout) { Remove-Item $msixLayout -Recurse -Force }
New-Item -ItemType Directory -Path $msixLayout -Force | Out-Null

Get-ChildItem $SourceDir -Force | ForEach-Object {
    Copy-Item $_.FullName $msixLayout -Recurse -Force
}
New-Item -ItemType Directory -Path (Join-Path $msixLayout "Assets") -Force | Out-Null

$assetsDir = Join-Path $msixLayout "Assets"
$ico = Join-Path $ProjectRoot "content\assets\SmartPerformanceDoctor.ico"
if (Test-Path $ico) {
    foreach ($name in @("StoreLogo.png", "Square44x44Logo.png", "Square150x150Logo.png")) {
        Copy-Item $ico (Join-Path $assetsDir $name) -Force
    }
}

$manifestSrc = Join-Path $ProjectRoot "artifacts\installer\templates\msix\Package.appxmanifest"
if (-not (Test-Path $manifestSrc)) {
    $manifestSrc = Join-Path $ProjectRoot "installer\msix\Package.appxmanifest"
}
$manifestDest = Join-Path $msixLayout "AppxManifest.xml"
$manifest = Get-Content $manifestSrc -Raw -Encoding UTF8
$manifest = $manifest -replace '(<Identity[^>]*Version=")[^"]+(")', "`${1}$Version`${2}"
$manifest = $manifest -replace 'SmartPerformanceDoctor\.App\.exe', 'SmartPerformanceDoctor.exe'
$manifest = $manifest -replace 'MinVersion="[\d.]+"', 'MinVersion="10.0.22000.0"'
$manifest = $manifest -replace 'MaxVersionTested="[\d.]+"', 'MaxVersionTested="10.0.26100.0"'
$manifest = $manifest -replace 'Description="[^"]*"', 'Description="Windows 11 PC 점검 및 안전 복구 도우미"'
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($manifestDest, $manifest, $utf8NoBom)

$makeappx = Find-MakeAppx
$msixName = "SmartPerformanceDoctor_v$($Version -replace '\.','_').msix"
$msixPath = Join-Path $OutputDir $msixName

if ($makeappx) {
    & $makeappx pack /d $msixLayout /p $msixPath /o
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $msixPath)) {
        throw "MSIX 패키지 생성에 실패했습니다. AppxManifest.xml을 확인하세요."
    }
    Write-Host "[OK] MSIX: $msixPath" -ForegroundColor Green
    Write-Host "[INFO] 서명: .\scripts\sign-consumer.ps1 -SkipIfNoCert" -ForegroundColor Gray
} else {
    Write-Host "[OK] MSIX layout: $msixLayout" -ForegroundColor Green
    Write-Host "[PENDING] Windows SDK의 makeappx.exe 설치 후 동일 스크립트를 다시 실행하세요." -ForegroundColor Yellow
}