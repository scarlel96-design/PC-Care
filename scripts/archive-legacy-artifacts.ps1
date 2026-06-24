$ErrorActionPreference = "Stop"

Write-Host "== Legacy artifacts archive ==" -ForegroundColor Cyan

$archiveRoot = ".\archive\legacy"
New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null

$patterns = @(
    "STABILITY_AUDIT_v*.md",
    "ENGINE_PACK_MANIFEST_v*.json",
    "SmartPerformanceDoctor.v*.slnx"
)

$moved = 0
foreach ($pattern in $patterns) {
    Get-ChildItem -Path "." -Filter $pattern -File | ForEach-Object {
        $dest = Join-Path $archiveRoot $_.Name
        if (Test-Path $dest) {
            Remove-Item $dest -Force
        }
        Move-Item $_.FullName $dest -Force
        $moved++
        Write-Host "  archived: $($_.Name)" -ForegroundColor Gray
    }
}

$index = @(
    "# Legacy Archive",
    "",
    "v16-v43 개발 산출물이 여기로 이동되었습니다.",
    "현재 제품 버전: 44.0.0",
    "",
    "이동된 항목:",
    "- STABILITY_AUDIT_v*.md",
    "- ENGINE_PACK_MANIFEST_v*.json",
    "- SmartPerformanceDoctor.v*.slnx",
    "",
    "이동 일시: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
) -join "`n"

Set-Content (Join-Path $archiveRoot "README.md") $index -Encoding UTF8

Write-Host "[OK] Archived $moved legacy files to $archiveRoot" -ForegroundColor Green