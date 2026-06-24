$ErrorActionPreference = "Stop"

Write-Host "== Collect error bundle ==" -ForegroundColor Cyan

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outRoot = ".\artifacts\error-bundles"
$stage = Join-Path $outRoot "stage_$stamp"
$zip = Join-Path $outRoot "SmartPerformanceDoctor_ErrorBundle_$stamp.zip"

if (Test-Path $stage) {
    Remove-Item $stage -Recurse -Force
}

New-Item -ItemType Directory -Path $stage -Force | Out-Null

$paths = @(
    "artifacts\logs",
    "artifacts\rc",
    "artifacts\release",
    "$env:USERPROFILE\Desktop\SmartPerformanceDoctor\CrashLogs",
    "docs",
    "scripts",
    "STABILITY_AUDIT_v31.md",
    "ENGINE_PACK_MANIFEST_v31.json"
)

foreach ($path in $paths) {
    if (Test-Path $path) {
        Copy-Item $path $stage -Recurse -Force
    }
}

@"
Timestamp: $(Get-Date -Format o)
OS: $([System.Environment]::OSVersion)
Machine: $env:COMPUTERNAME
User: $env:USERNAME
PowerShell: $($PSVersionTable.PSVersion)
"@ | Set-Content (Join-Path $stage "ENVIRONMENT.txt") -Encoding UTF8

New-Item -ItemType Directory -Path $outRoot -Force | Out-Null
Compress-Archive -Path "$stage\*" -DestinationPath $zip -Force
Remove-Item $stage -Recurse -Force

Write-Host "[OK] Error bundle: $zip" -ForegroundColor Green
