function Get-RuntimeRoot {
    param([string]$ProjectRoot = "")
    if (-not $ProjectRoot) {
        $ProjectRoot = Split-Path $PSScriptRoot -Parent
    }

    return $ProjectRoot
}

function Get-RuntimeDirectoryNames {
    return @('engine', 'runtimes', 'Views', 'Controls', 'Resources', 'Styles')
}

function Get-RuntimeRootFileNames {
    return @(
        'PCCare.exe',
        'PCCare.deps.json',
        'PCCare.runtimeconfig.json',
        'PCCare.pri',
        'SmartPerformanceDoctor.exe',
        'SmartPerformanceDoctor.dll',
        'SmartPerformanceDoctor.deps.json',
        'SmartPerformanceDoctor.runtimeconfig.json',
        'SmartPerformanceDoctor.pri',
        'AstraCare.exe',
        'AegisRecoveryHelper.exe',
        'AegisRecoveryService.exe',
        'README.txt'
    )
}

function Test-RuntimePublished {
    param([string]$RuntimeRoot)
    Test-Path (Join-Path $RuntimeRoot 'PCCare.exe')
}

function Copy-RuntimeTreeToStage {
    param(
        [string]$RuntimeRoot,
        [string]$StageDir
    )

    New-Item -ItemType Directory -Path $StageDir -Force | Out-Null

    foreach ($dirName in Get-RuntimeDirectoryNames) {
        $src = Join-Path $RuntimeRoot $dirName
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $StageDir $dirName) -Recurse -Force
        }
    }

    $contentSrc = Join-Path $RuntimeRoot 'content'
    if (Test-Path $contentSrc) {
        Copy-Item $contentSrc (Join-Path $StageDir 'content') -Recurse -Force
    }

    Get-ChildItem $RuntimeRoot -File -ErrorAction SilentlyContinue | Where-Object {
        ($_.Extension -in @('.exe', '.dll', '.json', '.pri', '.txt', '.xbf')) -and
        ($_.Name -notlike 'SmartPerformanceDoctor.Setup*') -and
        ($_.Name -notin @('AstraCare.exe', 'AstraCare_Setup.exe'))
    } | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $StageDir $_.Name) -Force
    }
}

function Remove-LegacyRuntimeDist {
    param([string]$ProjectRoot)

    $legacy = Join-Path $ProjectRoot 'dist\AstraCare'
    if (Test-Path $legacy) {
        Remove-Item $legacy -Recurse -Force
        Write-Host "[CLEAN] legacy dist\AstraCare\" -ForegroundColor DarkGray
    }

    $dist = Join-Path $ProjectRoot 'dist'
    if (Test-Path $dist) {
        $remaining = Get-ChildItem $dist -Force -ErrorAction SilentlyContinue
        if (-not $remaining) {
            Remove-Item $dist -Force -ErrorAction SilentlyContinue
        }
    }
}

function Remove-RuntimePublishArtifacts {
    param([string]$ProjectRoot)

    foreach ($dirName in Get-RuntimeDirectoryNames) {
        $path = Join-Path $ProjectRoot $dirName
        if (Test-Path $path) {
            Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    foreach ($name in Get-RuntimeRootFileNames) {
        $path = Join-Path $ProjectRoot $name
        if (Test-Path $path) {
            Remove-Item $path -Force -ErrorAction SilentlyContinue
        }
    }

    Get-ChildItem $ProjectRoot -File -Filter '*.dll' -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $ProjectRoot -File -Filter '*.xbf' -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $ProjectRoot -File -Filter '*.deps.json' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'PCCare*' -or $_.Name -like 'SmartPerformanceDoctor*' -or $_.Name -like 'AegisRecovery*' } |
        Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $ProjectRoot -File -Filter '*.runtimeconfig.json' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'PCCare*' -or $_.Name -like 'SmartPerformanceDoctor*' -or $_.Name -like 'AegisRecovery*' } |
        Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem $ProjectRoot -File -Filter 'PCCare.pri' -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
}