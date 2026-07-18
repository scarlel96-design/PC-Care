param(
    [string]$PayloadDir = "",
    [switch]$SkipIfNoCert,
    [switch]$RequireSigned
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
if (-not $PayloadDir) {
    $PayloadDir = Get-RuntimeRoot -ProjectRoot $ProjectRoot
}

if (-not (Test-Path $PayloadDir)) {
    throw "Payload directory not found: $PayloadDir"
}

$env:SPD_SIGN_CERT_PATH = if ($env:SPD_SIGN_CERT_PATH) {
    $env:SPD_SIGN_CERT_PATH
} else {
    Join-Path $ProjectRoot "artifacts\signing\SmartPerformanceDoctor-dev.pfx"
}
if (-not $env:SPD_SIGN_CERT_PASSWORD) {
    $env:SPD_SIGN_CERT_PASSWORD = "SmartPerformanceDoctor-Dev-2026"
}

if (-not (Test-Path $env:SPD_SIGN_CERT_PATH)) {
    Write-Host "[INFO] Dev signing certificate not found. Creating one..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "create-dev-signing-cert.ps1") -OutputPath $env:SPD_SIGN_CERT_PATH
}

function Find-SignTool {
    $cmd = Get-Command signtool -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $kits) {
        $found = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

$signtool = Find-SignTool
$certPath = $env:SPD_SIGN_CERT_PATH
if (-not (Test-Path $certPath)) {
    if ($SkipIfNoCert) {
        Write-Host "[SKIP] signing certificate not available for runtime payload." -ForegroundColor Yellow
        return
    }
    throw "Runtime signing certificate missing: $certPath"
}

function Import-SigningCertificate {
    param([string]$Path, [string]$Password)
    $secure = ConvertTo-SecureString $Password -AsPlainText -Force
    return Import-PfxCertificate -FilePath $Path -Password $secure -CertStoreLocation Cert:\CurrentUser\My -Exportable
}

$signingCert = Import-SigningCertificate -Path $certPath -Password $env:SPD_SIGN_CERT_PASSWORD
if (-not $signtool) {
    Write-Host "[WARN] signtool not found. Falling back to Set-AuthenticodeSignature." -ForegroundColor Yellow
}

$timestampUrl = if ($env:SPD_TIMESTAMP_URL) { $env:SPD_TIMESTAMP_URL } else { "http://timestamp.digicert.com" }
$payloadResolved = (Resolve-Path -LiteralPath $PayloadDir).Path
$projectResolved = (Resolve-Path -LiteralPath $ProjectRoot).Path
$publishInPlace = $payloadResolved.Equals($projectResolved, [StringComparison]::OrdinalIgnoreCase)

$runtimeRootExcludes = @(
    'AstraCare.exe',
    'AstraCare_Setup.exe',
    'SmartPerformanceDoctor_Setup.exe'
)

$targets = New-Object System.Collections.Generic.List[System.IO.FileInfo]
if ($publishInPlace) {
    Get-ChildItem $PayloadDir -File -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.Extension -in ".exe", ".dll") -and
            ($_.Name -notin $runtimeRootExcludes) -and
            ($_.Name -notlike 'SmartPerformanceDoctor_Setup*')
        } |
        ForEach-Object { $targets.Add($_) }
    foreach ($dirName in (Get-RuntimeDirectoryNames) + @('content', 'engine', 'runtimes')) {
        $dirPath = Join-Path $PayloadDir $dirName
        if (-not (Test-Path $dirPath)) { continue }
        Get-ChildItem $dirPath -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in ".exe", ".dll" } |
            ForEach-Object { $targets.Add($_) }
    }
}
else {
    Get-ChildItem $PayloadDir -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in ".exe", ".dll" } |
        ForEach-Object { $targets.Add($_) }
}

$targets = $targets | Sort-Object FullName -Unique

$signed = 0
$failed = 0
foreach ($target in $targets) {
    Write-Host "Signing $($target.FullName.Substring($PayloadDir.Length).TrimStart('\'))" -ForegroundColor Gray
    $ok = $false
    if ($signtool) {
        & $signtool sign /fd SHA256 /f $certPath /p $env:SPD_SIGN_CERT_PASSWORD /tr $timestampUrl /td SHA256 $target.FullName
        $ok = $LASTEXITCODE -eq 0
    }
    if (-not $ok) {
        try {
            $sig = Set-AuthenticodeSignature -FilePath $target.FullName -Certificate $signingCert -HashAlgorithm SHA256 -TimestampServer $timestampUrl
            $ok = $sig.Status -eq "Valid"
        }
        catch {
            $ok = $false
        }
    }
    if (-not $ok) {
        $failed++
        if ($RequireSigned) {
            throw "Failed to sign: $($target.FullName)"
        }
        continue
    }
    $signed++
}

if ($RequireSigned -and $failed -gt 0) {
    throw "Runtime payload signing failed for $failed file(s)."
}

Write-Host "[OK] Signed $signed runtime binary file(s) under $PayloadDir" -ForegroundColor Green