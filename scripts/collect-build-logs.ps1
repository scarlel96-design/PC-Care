$ErrorActionPreference = "Stop"

$root = Resolve-Path "."
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$out = Join-Path $root "artifacts\SmartPerformanceDoctor_BuildLogs_$stamp.zip"
$stage = Join-Path $root "artifacts\buildlog_stage_$stamp"

if (Test-Path $stage) {
    Remove-Item $stage -Recurse -Force
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null

$items = @(
    "artifacts\logs",
    "STABILITY_AUDIT_v23.md",
    "ENGINE_PACK_MANIFEST_v23.json",
    "scripts",
    "docs"
)

foreach ($item in $items) {
    if (Test-Path $item) {
        Copy-Item $item $stage -Recurse -Force
    }
}

Compress-Archive -Path "$stage\*" -DestinationPath $out -Force
Remove-Item $stage -Recurse -Force

Write-Host "[OK] Build logs archive: $out" -ForegroundColor Green
