$ErrorActionPreference = "Stop"

Write-Host "== Verify release checksums ==" -ForegroundColor Cyan

$release = ".\artifacts\release"
$sumFile = Join-Path $release "SHA256SUMS.txt"

if (-not (Test-Path $sumFile)) {
    throw "SHA256SUMS.txt가 없습니다. 먼저 .\scripts\new-release-manifest.ps1을 실행하세요."
}

$publish = ".\artifacts\publish\SmartPerformanceDoctor"
if (-not (Test-Path $publish)) {
    throw "publish 폴더가 없습니다."
}

$failed = $false

Get-Content $sumFile | ForEach-Object {
    if (-not $_.Trim()) { return }
    $parts = $_ -split "\\s+", 2
    if ($parts.Count -lt 2) { return }

    $expected = $parts[0].Trim().ToLower()
    $relative = $parts[1].Trim()
    $path = Join-Path $publish $relative.Replace("/", "\")

    if (-not (Test-Path $path)) {
        Write-Host "[MISSING] $relative" -ForegroundColor Red
        $script:failed = $true
        return
    }

    $actual = (Get-FileHash $path -Algorithm SHA256).Hash.ToLower()
    if ($actual -eq $expected) {
        Write-Host "[OK] $relative" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] $relative" -ForegroundColor Red
        $script:failed = $true
    }
}

if ($failed) {
    throw "Checksum verification failed."
}

Write-Host "[OK] Checksum verification completed." -ForegroundColor Green
