$ErrorActionPreference = "Stop"

Write-Host "== Publish local package ==" -ForegroundColor Cyan

$publish = ".\artifacts\publish\SmartPerformanceDoctor"
if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}
New-Item -ItemType Directory -Path $publish | Out-Null

$appOutCandidates = @(
    ".\src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0",
    ".\src\SmartPerformanceDoctor.App\bin\Release\net10.0-windows10.0.26100.0"
)

$appOut = $appOutCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $appOut) {
    throw "App 출력 폴더를 찾지 못했습니다. 먼저 .\scripts\build.ps1을 실행하세요."
}

Copy-Item "$appOut\*" $publish -Recurse -Force

if (Test-Path ".\rules") {
    Copy-Item ".\rules" "$publish\rules" -Recurse -Force
}
if (Test-Path ".\assets") {
    Copy-Item ".\assets" "$publish\assets" -Recurse -Force
}
# Consumer builds should not ship developer documentation.

Get-ChildItem $publish -File -Recurse | ForEach-Object {
    $hash = Get-FileHash $_.FullName -Algorithm SHA256
    "$($hash.Hash.ToLower())  $($_.FullName.Substring((Resolve-Path $publish).Path.Length + 1))"
} | Set-Content "$publish\SHA256SUMS.txt" -Encoding UTF8

Write-Host "[OK] Published: $publish" -ForegroundColor Green
