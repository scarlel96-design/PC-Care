$ErrorActionPreference = "Stop"

Write-Host "== Create update channel manifest ==" -ForegroundColor Cyan

$release = ".\artifacts\release"
New-Item -ItemType Directory -Path $release -Force | Out-Null

$portableFile = Get-ChildItem $release -Filter "SmartPerformanceDoctor*_Portable.zip" -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$portable = if ($portableFile) { $portableFile.FullName } else { Join-Path $release "SmartPerformanceDoctor_v43_Portable.zip" }
$version = "43.0.0"

$portableInfo = $null
if (Test-Path $portable) {
    $hash = Get-FileHash $portable -Algorithm SHA256
    $portableInfo = [PSCustomObject]@{
        file = Split-Path $portable -Leaf
        size = (Get-Item $portable).Length
        sha256 = $hash.Hash.ToLower()
    }
}

$channel = [PSCustomObject]@{
    product = "Smart Performance Doctor"
    channel = "stable"
    latestVersion = $version
    minimumSupportedVersion = "30.0.0"
    createdAt = (Get-Date).ToString("o")
    releaseNotes = "v44 consumer release with SQLite knowledge base and rules-driven intelligence"
    artifacts = @{
        portable = $portableInfo
    }
    safety = @{
        requiresManualInstall = $true
        autoUpdateEnabled = $false
        checksumRequired = $true
        signatureRecommended = $true
    }
}

$path = Join-Path $release "UPDATE_CHANNEL.json"
$channel | ConvertTo-Json -Depth 8 | Set-Content $path -Encoding UTF8

Write-Host "[OK] Update channel: $path" -ForegroundColor Green
