param(
    [switch]$SkipIfNoCert
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

Write-Host "== Sign consumer release artifacts ==" -ForegroundColor Cyan

function Find-SignTool {
    $cmd = Get-Command signtool -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $kits) {
        $found = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    return $null
}

$signtool = Find-SignTool
if (-not $signtool) {
    if ($SkipIfNoCert) {
        Write-Host "[SKIP] signtool을 찾지 못했습니다. Windows SDK 설치 후 다시 시도하세요." -ForegroundColor Yellow
        return
    }
    throw "signtool을 찾지 못했습니다. Windows SDK 설치가 필요합니다."
}

$certPath = $env:SPD_SIGN_CERT_PATH
$certPassword = $env:SPD_SIGN_CERT_PASSWORD
$timestampUrl = if ($env:SPD_TIMESTAMP_URL) { $env:SPD_TIMESTAMP_URL } else { "http://timestamp.digicert.com" }

if ($certPath -and -not [System.IO.Path]::IsPathRooted($certPath)) {
    $certPath = Join-Path $ProjectRoot $certPath
}

if (-not $certPath -or -not (Test-Path $certPath)) {
    if ($SkipIfNoCert) {
        Write-Host "[SKIP] SPD_SIGN_CERT_PATH 인증서가 없습니다. .\scripts\create-dev-signing-cert.ps1 로 개발용 인증서를 만들 수 있습니다." -ForegroundColor Yellow
        return
    }
    throw "SPD_SIGN_CERT_PATH 환경변수에 서명 인증서(.pfx) 경로가 필요합니다."
}

$targets = @()
$manifestPath = Join-Path $ProjectRoot ".consumer-publish-manifest.json"
if (Test-Path $manifestPath) {
    $manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($name in $manifest.files) {
        $path = Join-Path $ProjectRoot ($name -replace '/', '\')
        if (Test-Path $path) { $targets += Get-Item $path }
    }
}

$installerRoots = @(
    (Join-Path $ProjectRoot "artifacts\installer"),
    (Join-Path $ProjectRoot "artifacts\publish\SmartPerformanceDoctor"),
    (Join-Path $ProjectRoot "artifacts\release")
)
foreach ($root in $installerRoots) {
    if (Test-Path $root) {
        $targets += Get-ChildItem $root -File -Recurse -Include *.exe,*.dll,*.msi,*.msix,*.msixbundle -ErrorAction SilentlyContinue
    }
}

$targets = $targets | Sort-Object FullName -Unique

if ($targets.Count -eq 0) {
    Write-Host "[SKIP] 서명할 파일이 없습니다. 먼저 publish-consumer.ps1을 실행하세요." -ForegroundColor Yellow
    return
}

foreach ($target in $targets) {
    Write-Host "Signing $($target.FullName)" -ForegroundColor Gray
    & $signtool sign /fd SHA256 /td SHA256 /tr $timestampUrl /f $certPath /p $certPassword $target.FullName
}

Write-Host "[OK] Signed $($targets.Count) files." -ForegroundColor Green