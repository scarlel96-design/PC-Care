$ErrorActionPreference = "Stop"

Write-Host "== Package portable release ==" -ForegroundColor Cyan

.\scripts\repair-portable-layout.ps1

$publish = ".\artifacts\publish\SmartPerformanceDoctor"
$release = ".\artifacts\release"

if (-not (Test-Path $publish)) {
    throw "publish 폴더가 없습니다. 먼저 .\scripts\publish-local.ps1을 실행하세요."
}

New-Item -ItemType Directory -Path $release -Force | Out-Null

$zip = Join-Path $release "SmartPerformanceDoctor_v44_Portable.zip"
if (Test-Path $zip) {
    Remove-Item $zip -Force
}

Compress-Archive -Path "$publish\*" -DestinationPath $zip -Force

$hash = Get-FileHash $zip -Algorithm SHA256
[PSCustomObject]@{
    file = Split-Path $zip -Leaf
    sha256 = $hash.Hash.ToLower()
    size = (Get-Item $zip).Length
    createdAt = (Get-Date).ToString("o")
} | ConvertTo-Json | Set-Content (Join-Path $release "PORTABLE_PACKAGE.json") -Encoding UTF8

Write-Host "[OK] Portable package: $zip" -ForegroundColor Green
