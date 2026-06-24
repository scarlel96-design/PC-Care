$ErrorActionPreference = "Stop"

Write-Host "== Create v42 release artifact manifest ==" -ForegroundColor Cyan

$out = ".\artifacts\release"
New-Item -ItemType Directory -Path $out -Force | Out-Null

$paths = @(
    ".\artifacts\publish\SmartPerformanceDoctor",
    ".\artifacts\release",
    ".\artifacts\installer",
    ".\portable",
    ".\installer"
)

$items = @()
foreach ($path in $paths) {
    if (-not (Test-Path $path)) { continue }
    Get-ChildItem $path -File -Recurse | ForEach-Object {
        $hash = Get-FileHash $_.FullName -Algorithm SHA256
        $items += [PSCustomObject]@{
            name = $_.Name
            path = $_.FullName
            relativePath = Resolve-Path $_.FullName -Relative
            size = $_.Length
            sha256 = $hash.Hash.ToLower()
            modified = $_.LastWriteTime.ToString("o")
        }
    }
}

$manifest = [PSCustomObject]@{
    product = "Smart Performance Doctor"
    version = "42.0.0"
    createdAt = (Get-Date).ToString("o")
    artifactCount = $items.Count
    artifacts = $items
}

$manifest | ConvertTo-Json -Depth 8 | Set-Content "$out\RELEASE_ARTIFACT_MANIFEST_v42.json" -Encoding UTF8
Write-Host "[OK] $out\RELEASE_ARTIFACT_MANIFEST_v42.json" -ForegroundColor Green
