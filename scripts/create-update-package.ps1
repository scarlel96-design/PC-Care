param(
    [string]$FromVersion = "44.3.0",
    [string]$ToVersion = "44.3.0",
    [string]$AppOut = "",
    [string]$ChangesFile = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

if (-not $AppOut) {
    $AppOut = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0"
    if (-not (Test-Path (Join-Path $AppOut "SmartPerformanceDoctor.exe")) -and (Test-Path (Join-Path $ProjectRoot "SmartPerformanceDoctor.exe"))) {
        $AppOut = $ProjectRoot
    }
}

$runtimeDirs = @("engine", "content", "runtimes", "Views", "Controls", "Resources", "Styles")
$runtimeExtensions = @(".exe", ".dll", ".json", ".pri", ".xbf", ".bat", ".txt", ".ico")
$isProjectRootSource = (Resolve-Path $AppOut).Path -eq (Resolve-Path $ProjectRoot).Path

if (-not (Test-Path $AppOut)) {
    throw "앱 출력 폴더를 찾지 못했습니다. 먼저 build.ps1 를 실행하세요: $AppOut"
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $ProjectRoot "dist\updates"
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$changes = @(
    "spdup update package support",
    "update page progress gauge and changelog"
)
if ($ChangesFile -and (Test-Path $ChangesFile)) {
    $doc = Get-Content $ChangesFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($doc.changes) { $changes = @($doc.changes) }
    if ($doc.releaseNotes) { $releaseNotes = [string]$doc.releaseNotes } else { $releaseNotes = "Smart Performance Doctor $ToVersion" }
    if ($doc.fromVersion) { $FromVersion = [string]$doc.fromVersion }
    if ($doc.toVersion) { $ToVersion = [string]$doc.toVersion }
} else {
    $releaseNotes = "Smart Performance Doctor $ToVersion 업데이트"
}

$stage = Join-Path $env:TEMP "spd_update_stage_$([Guid]::NewGuid().ToString('N'))"
$payload = Join-Path $stage "payload"
New-Item -ItemType Directory -Path $payload -Force | Out-Null

function Copy-RuntimePayload {
    param([string]$Source, [string]$Destination)
    if ($isProjectRootSource) {
        Get-ChildItem $Source -File | Where-Object {
            $runtimeExtensions -contains $_.Extension.ToLowerInvariant()
        } | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $Destination $_.Name) -Force
        }
        foreach ($dirName in $runtimeDirs) {
            $src = Join-Path $Source $dirName
            if (Test-Path $src) {
                Copy-Item $src (Join-Path $Destination $dirName) -Recurse -Force
            }
        }
        return
    }

    Get-ChildItem $Source -Recurse -File | Where-Object { $_.Extension -ne ".pdb" } | ForEach-Object {
        $relative = $_.FullName.Substring($Source.Length + 1)
        $dest = Join-Path $Destination $relative
        $destDir = Split-Path $dest -Parent
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
        Copy-Item $_.FullName $dest -Force
    }
}

Copy-RuntimePayload -Source $AppOut -Destination $payload

$fileEntries = New-Object System.Collections.Generic.List[object]
Get-ChildItem $payload -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($payload.Length + 1).Replace('\', '/')
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
    $fileEntries.Add([PSCustomObject]@{
        path = "payload/$relative"
        sha256 = $hash
    })
}

$manifest = [PSCustomObject]@{
    format = "spd-update-v1"
    product = "Smart Performance Doctor"
    channel = "stable"
    fromVersion = $FromVersion
    toVersion = $ToVersion
    minimumSupportedVersion = "44.0.0"
    createdAt = (Get-Date).ToString("o")
    releaseNotes = $releaseNotes
    changes = $changes
    requiresRestart = $true
    packageSha256 = ""
    files = $fileEntries
}

$fingerprintSource = ($fileEntries | Sort-Object path | ForEach-Object { "{0}:{1}" -f $_.path.ToLower(), $_.sha256 }) -join "|"
$manifest.packageSha256 = [BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($fingerprintSource))
).Replace("-", "").ToLower()

$manifestPath = Join-Path $stage "update.manifest.json"
$manifest | ConvertTo-Json -Depth 8 | Set-Content $manifestPath -Encoding UTF8

$packageName = "SmartPerformanceDoctor_v${ToVersion}_Update.spdup"
$packagePath = Join-Path $OutputDir $packageName
$zipPath = [System.IO.Path]::ChangeExtension($packagePath, ".zip")
if (Test-Path $packagePath) { Remove-Item $packagePath -Force }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath -Force
Move-Item $zipPath $packagePath -Force

Remove-Item $stage -Recurse -Force

$finalHash = (Get-FileHash $packagePath -Algorithm SHA256).Hash.ToLower()
Write-Host "[OK] Update package: $packagePath" -ForegroundColor Green
Write-Host "[OK] Package SHA256: $finalHash" -ForegroundColor Green
Write-Host "[OK] Files: $($fileEntries.Count)" -ForegroundColor Green