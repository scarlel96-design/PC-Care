$ErrorActionPreference = "Stop"

Write-Host "== v32 Release Gate ==" -ForegroundColor Cyan

.\scripts\run-rc-validation.ps1
.\scripts\new-release-manifest.ps1
.\scripts\verify-checksums.ps1
.\scripts\package-portable.ps1
.\scripts\new-update-channel.ps1

if ($env:SPD_SIGN_CERT_PATH) {
    .\scripts\sign-release.ps1
    .\scripts\verify-signatures.ps1
} else {
    Write-Host "[WARN] SPD_SIGN_CERT_PATH 없음. 서명 단계는 건너뜁니다." -ForegroundColor Yellow
}

.\scripts\collect-error-bundle.ps1

Write-Host "[OK] Release gate completed." -ForegroundColor Green
