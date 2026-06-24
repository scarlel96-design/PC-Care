$ErrorActionPreference = "Stop"

Write-Host "== Verify signatures ==" -ForegroundColor Cyan

if (-not (Get-Command signtool -ErrorAction SilentlyContinue)) {
    throw "signtool을 찾지 못했습니다. Windows SDK 설치가 필요합니다."
}

$targets = @()
$publish = ".\artifacts\publish\SmartPerformanceDoctor"
$release = ".\artifacts\release"

if (Test-Path $publish) {
    $targets += Get-ChildItem $publish -File -Recurse -Include *.exe,*.dll,*.msi,*.msix
}
if (Test-Path $release) {
    $targets += Get-ChildItem $release -File -Recurse -Include *.exe,*.dll,*.msi,*.msix
}

$failed = $false
foreach ($target in $targets) {
    Write-Host "Verifying $($target.FullName)" -ForegroundColor Gray
    signtool verify /pa /all $target.FullName
    if ($LASTEXITCODE -ne 0) {
        $failed = $true
    }
}

if ($failed) {
    throw "Signature verification failed."
}

Write-Host "[OK] Signature verification completed." -ForegroundColor Green
