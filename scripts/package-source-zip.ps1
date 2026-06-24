param(
    [string]$Version = "50.0.0",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDir) {
    $codingRoot = Split-Path (Split-Path $ProjectRoot -Parent) -Parent
    $OutputDir = Join-Path $codingRoot "PCCare_Pro_v$($Version -replace '\.','_')_Handoff"
}

$zipName = "PCCare_Source_v$Version.zip"
$zipPath = Join-Path $OutputDir $zipName
$stage = Join-Path $env:TEMP "PCCare_Source_stage_$(Get-Random)"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
$excludeRootDirs = @(
    'bin', 'obj', 'target', 'dist', 'artifacts', '.git', '.vs', 'node_modules', 'terminals', 'agent-tools'
) + @(Get-RuntimeDirectoryNames)
$excludeFilePatterns = @('installer-layout.zip', 'installer.msi', '*.user', '*.suo', '*.pdb')

Get-ChildItem $ProjectRoot -Force | ForEach-Object {
    if ($excludeRootDirs -contains $_.Name) { return }
    if ($_.Name -eq 'AstraCare_Setup.exe') { return }
    Copy-Item $_.FullName -Destination (Join-Path $stage $_.Name) -Recurse -Force
}

Get-ChildItem $stage -Recurse -Directory -Force | Where-Object { $excludeRootDirs -contains $_.Name } | ForEach-Object {
    Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
}

foreach ($name in Get-RuntimeRootFileNames) {
    $path = Join-Path $stage $name
    if (Test-Path $path) { Remove-Item $path -Force -ErrorAction SilentlyContinue }
}
Get-ChildItem $stage -File -Filter '*.dll' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem $stage -File -Filter '*.xbf' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

foreach ($pattern in $excludeFilePatterns) {
    Get-ChildItem $stage -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item $_.FullName -Force
    }
}

# Setup resources: keep scripts to rebuild, drop embedded installer payloads
$setupResources = Join-Path $stage "src\SmartPerformanceDoctor.Setup\Resources"
if (Test-Path $setupResources) {
    Get-ChildItem $setupResources -File | Where-Object {
        $_.Name -in @('installer-layout.zip', 'installer.msi')
    } | Remove-Item -Force
}

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "[OK] Source ZIP: $zipPath ($sizeMb MB)" -ForegroundColor Green
Write-Host "[OK] Excluded: bin/obj/dist/artifacts, installer-layout.zip, installer.msi" -ForegroundColor Green