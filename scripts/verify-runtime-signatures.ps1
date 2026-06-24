param(
    [string]$PayloadDir = "",
    [switch]$RequireSigned
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
if (-not $PayloadDir) {
    . (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
    $PayloadDir = Get-RuntimeRoot -ProjectRoot $ProjectRoot
}

if (-not (Test-Path $PayloadDir)) {
    throw "Payload directory not found: $PayloadDir"
}

$critical = @(
    "PCCare.exe",
    "SmartPerformanceDoctor.exe",

    "SmartPerformanceDoctor.dll",
    "engine\smart_performance_doctor_core.exe",
    "engine\smart_performance_doctor_repair_helper.exe",
    "engine\AstraCore.exe",
    "engine\AstraRepairHelper.exe",
    "engine\AegisRecoveryHelper.exe",
    "engine\AegisRecoveryService.exe"
)

Write-Host "== Verify runtime Authenticode signatures ==" -ForegroundColor Cyan
$unsigned = New-Object System.Collections.Generic.List[string]
$untrusted = New-Object System.Collections.Generic.List[string]
$trustedMarkers = @("Smart Performance Doctor Dev", "PC Care", "PCCare")

foreach ($rel in $critical) {
    $path = Join-Path $PayloadDir $rel
    if (-not (Test-Path $path)) {
        continue
    }

    $sig = Get-AuthenticodeSignature -FilePath $path
    if ($sig.Status -ne "Valid") {
        $unsigned.Add("$rel ($($sig.Status))")
        continue
    }

    $subject = $sig.SignerCertificate.Subject
    $trusted = $false
    foreach ($marker in $trustedMarkers) {
        if ($subject -like "*$marker*") {
            $trusted = $true
            break
        }
    }
    if (-not $trusted) {
        $untrusted.Add("$rel ($subject)")
    }
}

if ($unsigned.Count -gt 0) {
    Write-Host "[WARN] Unsigned or invalid signatures:" -ForegroundColor Yellow
    $unsigned | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    if ($RequireSigned) {
        throw "Runtime signature verification failed: $($unsigned.Count) file(s)."
    }
}

if ($untrusted.Count -gt 0) {
    Write-Host "[WARN] Untrusted publisher:" -ForegroundColor Yellow
    $untrusted | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    if ($RequireSigned) {
        throw "Untrusted publisher on $($untrusted.Count) file(s)."
    }
}

if ($unsigned.Count -eq 0 -and $untrusted.Count -eq 0) {
    Write-Host "[OK] Critical runtime binaries are Authenticode-signed." -ForegroundColor Green
} else {
    Write-Host "[PENDING] Signature verification incomplete (dev build allowed)." -ForegroundColor Yellow
}