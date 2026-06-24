$ErrorActionPreference = "Continue"

$root = Resolve-Path "."
$logDir = Join-Path $root "artifacts\logs"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$transcript = Join-Path $logDir "build_diagnostics_$stamp.txt"

Start-Transcript -Path $transcript -Force

Write-Host "== Smart Performance Doctor v23 Build Diagnostics ==" -ForegroundColor Cyan

Write-Host "`n== Environment =="
try { .\scripts\check-environment.ps1 } catch { Write-Host "[ENV ERROR] $_" -ForegroundColor Red }

Write-Host "`n== Source Verification =="
try { .\scripts\verify-source.ps1 } catch { Write-Host "[VERIFY ERROR] $_" -ForegroundColor Red }

Write-Host "`n== Rust Build =="
try { .\scripts\build-core.ps1 } catch { Write-Host "[RUST BUILD ERROR] $_" -ForegroundColor Red }

Write-Host "`n== App Build =="
try { .\scripts\build-app.ps1 } catch { Write-Host "[APP BUILD ERROR] $_" -ForegroundColor Red }

Write-Host "`n== Core Smoke =="
try { .\scripts\run-core-smoke.ps1 } catch { Write-Host "[CORE SMOKE ERROR] $_" -ForegroundColor Red }

Write-Host "`n== RepairHelper Smoke =="
try { .\scripts\run-repairhelper-smoke.ps1 } catch { Write-Host "[REPAIRHELPER SMOKE ERROR] $_" -ForegroundColor Red }

Stop-Transcript

Write-Host "[OK] Diagnostics log: $transcript" -ForegroundColor Green


Write-Host "`n== Runtime Layout Diagnostics =="
try { .\scripts\run-app-diagnostics.ps1 } catch { Write-Host "[APP DIAGNOSTICS ERROR] $_" -ForegroundColor Red }
