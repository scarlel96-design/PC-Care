$ErrorActionPreference = "Stop"

Write-Host "== Create release manifest ==" -ForegroundColor Cyan

$publish = ".\artifacts\publish\SmartPerformanceDoctor"
$release = ".\artifacts\release"
New-Item -ItemType Directory -Path $release -Force | Out-Null

if (-not (Test-Path $publish)) {
    throw "publish 폴더가 없습니다. 먼저 .\scripts\publish-local.ps1을 실행하세요."
}

$files = Get-ChildItem $publish -File -Recurse | ForEach-Object {
    $hash = Get-FileHash $_.FullName -Algorithm SHA256
    [PSCustomObject]@{
        path = $_.FullName.Substring((Resolve-Path $publish).Path.Length + 1).Replace("\", "/")
        size = $_.Length
        sha256 = $hash.Hash.ToLower()
    }
}

$manifest = [PSCustomObject]@{
    name = "Smart Performance Doctor"
    version = "29.0.0"
    channel = "portable"
    createdAt = (Get-Date).ToString("o")
    files = $files
    requiredFiles = @(
        "smart_performance_doctor_core.exe",
        "smart_performance_doctor_repair_helper.exe"
    )
    safety = @{
        dryRunDefault = $true
        repairHelperRequired = $true
        allowlistOnly = $true
    }
}

$manifestPath = Join-Path $release "RELEASE_MANIFEST.json"
$manifest | ConvertTo-Json -Depth 8 | Set-Content $manifestPath -Encoding UTF8

Get-ChildItem $publish -File -Recurse | ForEach-Object {
    $hash = Get-FileHash $_.FullName -Algorithm SHA256
    "$($hash.Hash.ToLower())  $($_.FullName.Substring((Resolve-Path $publish).Path.Length + 1).Replace('\','/'))"
} | Set-Content (Join-Path $release "SHA256SUMS.txt") -Encoding UTF8

Write-Host "[OK] Manifest: $manifestPath" -ForegroundColor Green
