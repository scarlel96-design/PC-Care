function Get-RuntimeRoot {
    param([string]$ProjectRoot = "")
    if (-not $ProjectRoot) {
        $ProjectRoot = Split-Path $PSScriptRoot -Parent
    }

    return $ProjectRoot
}

function Get-AppPublishOutput {
    param([string]$ProjectRoot = "")
    if (-not $ProjectRoot) {
        $ProjectRoot = Split-Path $PSScriptRoot -Parent
    }

    $candidates = @(
        (Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish"),
        (Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0")
    )

    foreach ($candidate in $candidates) {
        $exe = Join-Path $candidate "SmartPerformanceDoctor.exe"
        $coreClr = Join-Path $candidate "coreclr.dll"
        if ((Test-Path -LiteralPath $exe) -and (Test-Path -LiteralPath $coreClr)) {
            return $candidate
        }
    }

    foreach ($candidate in $candidates) {
        $exe = Join-Path $candidate "SmartPerformanceDoctor.exe"
        if (Test-Path -LiteralPath $exe) {
            return $candidate
        }
    }

    return $candidates[0]
}

function Get-AppUiAssetSource {
    param([string]$ProjectRoot = "")
    if (-not $ProjectRoot) {
        $ProjectRoot = Split-Path $PSScriptRoot -Parent
    }

    $publish = (Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish")
    $build = (Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0")
    if (Test-Path -LiteralPath (Join-Path $publish "Views")) {
        return $publish
    }

    return $build
}

function Sync-PccarePublishBranding {
    param([string]$PublishDir)

    $mainExe = Join-Path $PublishDir "SmartPerformanceDoctor.exe"
    if (-not (Test-Path -LiteralPath $mainExe)) {
        throw "SmartPerformanceDoctor.exe missing in publish: $PublishDir"
    }

    Copy-Item $mainExe (Join-Path $PublishDir "PCCare.exe") -Force
    foreach ($pair in @(
            @{ Src = "SmartPerformanceDoctor.deps.json"; Dest = "PCCare.deps.json" },
            @{ Src = "SmartPerformanceDoctor.runtimeconfig.json"; Dest = "PCCare.runtimeconfig.json" },
            @{ Src = "SmartPerformanceDoctor.pri"; Dest = "PCCare.pri" }
        )) {
        $srcPath = Join-Path $PublishDir $pair.Src
        if (Test-Path -LiteralPath $srcPath) {
            Copy-Item $srcPath (Join-Path $PublishDir $pair.Dest) -Force
        }
    }

    Get-CommercialFolderLayoutText | Set-Content (Join-Path $PublishDir "FOLDER_LAYOUT.txt") -Encoding UTF8
    $readmePath = Join-Path $PublishDir "README.txt"
    if (-not (Test-Path -LiteralPath $readmePath)) {
        @(
            "PC 케어 프로",
            "",
            "Run: PCCare.exe",
            "",
            "See FOLDER_LAYOUT.txt for install folder structure."
        ) | Set-Content $readmePath -Encoding UTF8
    }
}

function Sync-WinUiThemeAssets {
    param(
        [string]$TargetDir,
        [string]$ProjectRoot = ""
    )

    if (-not $ProjectRoot) {
        $ProjectRoot = Split-Path $PSScriptRoot -Parent
    }

    $themeSrc = Join-Path $ProjectRoot "content\winui\Microsoft.UI.Xaml\Themes"
    if (-not (Test-Path -LiteralPath $themeSrc)) {
        throw "Missing vendored WinUI theme folder: $themeSrc"
    }

    $themeDest = Join-Path $TargetDir "Microsoft.UI.Xaml\Themes"
    New-Item -ItemType Directory -Force -Path $themeDest | Out-Null
    Copy-Item (Join-Path $themeSrc "*") $themeDest -Force

    foreach ($required in @("themeresources.xbf", "generic.xbf")) {
        if (-not (Test-Path (Join-Path $themeDest $required))) {
            throw "WinUI theme sync missing: $required"
        }
    }
}

function Sync-InstallLayoutUiAssets {
    param(
        [string]$LayoutDir,
        [string]$ProjectRoot = ""
    )

    if (-not $ProjectRoot) {
        $ProjectRoot = Split-Path $PSScriptRoot -Parent
    }

    $uiSource = Get-AppUiAssetSource -ProjectRoot $ProjectRoot
    foreach ($dirName in @("Views", "Controls", "Resources", "Styles")) {
        $srcDir = Join-Path $uiSource $dirName
        if (-not (Test-Path -LiteralPath $srcDir)) {
            throw "UI sync failed — build output missing $dirName. Run dotnet build -c Release -p:Platform=x64: $srcDir"
        }

        $destDir = Join-Path $LayoutDir $dirName
        if (Test-Path $destDir) {
            Remove-Item $destDir -Recurse -Force -ErrorAction SilentlyContinue
        }

        Copy-Item $srcDir $destDir -Recurse -Force
    }

    $shellXbfSources = @(
        $uiSource,
        (Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish"),
        (Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64"),
        (Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0")
    ) | Select-Object -Unique

    foreach ($name in @("App.xbf", "MainWindow.xbf")) {
        foreach ($src in $shellXbfSources) {
            if (-not (Test-Path -LiteralPath $src)) { continue }
            $file = Join-Path $src $name
            if (-not (Test-Path -LiteralPath $file)) { continue }
            Copy-Item $file (Join-Path $LayoutDir $name) -Force
            break
        }
    }

    Get-ChildItem $uiSource -File -Filter "*.xbf" -ErrorAction SilentlyContinue | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $LayoutDir $_.Name) -Force
    }
}

function Resolve-WindowsAppBootstrapPath {
    param([string]$Root)
    foreach ($relative in @(
            "runtimes\win-x64\native\Microsoft.WindowsAppRuntime.Bootstrap.dll",
            "Microsoft.WindowsAppRuntime.Bootstrap.dll"
        )) {
        $path = Join-Path $Root $relative
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }

    return $null
}

function Test-SelfContainedRuntimeLayout {
    param([string]$Root)
    foreach ($name in @("coreclr.dll", "hostfxr.dll", "hostpolicy.dll")) {
        if (-not (Test-Path (Join-Path $Root $name))) {
            return $false
        }
    }

    return $true
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
        'README.txt',
        'coreclr.dll',
        'hostfxr.dll',
        'hostpolicy.dll',
        'clrjit.dll'
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

function Get-CommercialBlockedRootFileNames {
    return @(
        'SmartPerformanceDoctor.exe',
        'SmartPerformanceDoctor.deps.json',
        'SmartPerformanceDoctor.runtimeconfig.json',
        'SmartPerformanceDoctor.pri',
        'AstraCare.exe',
        'AstraCare_Setup.exe',
        'start.bat',
        'INSTALLER_README.txt'
    )
}

function Get-CommercialBlockedEngineAliases {
    return @('AstraCore.exe', 'AstraRepairHelper.exe')
}

function Get-CommercialBlockedDirectoryNames {
    return @('artifacts', 'dist', 'data', 'assets', '.git', '.vscode', 'src', 'scripts', 'tests', 'target')
}

function Get-CommercialFolderLayoutText {
    return @(
        'PC 케어 프로 — 설치 폴더 구성',
        '',
        'PCCare.exe          프로그램 실행',
        'README.txt          사용 안내',
        'FOLDER_LAYOUT.txt   폴더 구성 안내',
        'engine/             진단·복구·보안 엔진',
        'content/            규칙·상업 데이터',
        'runtimes/           Windows App SDK / 네이티브 런타임',
        'coreclr.dll 등      .NET 런타임(설치본에 포함)',
        'Views/ Controls/ Resources/ Styles/  UI 리소스',
        '',
        '※ 개발용 파일(Setup, MSI, PDB, 소스)은 설치본에 포함되지 않습니다.'
    )
}

function Test-InstallLayoutRootFile {
    param([string]$FileName)
    return $FileName -in @(
        'PCCare.exe',
        'PCCare.deps.json',
        'PCCare.runtimeconfig.json',
        'PCCare.pri',
        'SmartPerformanceDoctor.dll',
        'SmartPerformanceDoctor.Data.dll',
        'SmartPerformanceDoctor.Contracts.dll',
        'SmartPerformanceDoctor.Aegis.dll',
        'README.txt',
        'FOLDER_LAYOUT.txt'
    ) -or $FileName -like '*.dll' -or $FileName -like '*.json' -or $FileName -like '*.xbf' -or $FileName -like '*.pri'
}

function Test-IsUnsafeSanitizeTarget {
    param(
        [string]$TargetDir,
        [string]$ProjectRoot
    )

    $targetResolved = (Resolve-Path -LiteralPath $TargetDir -ErrorAction Stop).Path
    $projectResolved = (Resolve-Path -LiteralPath $ProjectRoot -ErrorAction Stop).Path
    if ($targetResolved.Equals($projectResolved, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    foreach ($marker in @('src', 'scripts', 'tests', 'SmartPerformanceDoctor.sln', 'Cargo.toml', 'Cargo.lock')) {
        if (Test-Path (Join-Path $targetResolved $marker)) {
            return $true
        }
    }

    return $false
}

function Get-InstallLayoutViolations {
    param([string]$LayoutDir)

    $violations = New-Object System.Collections.Generic.List[string]

    foreach ($blocked in Get-CommercialBlockedRootFileNames) {
        if (Test-Path (Join-Path $LayoutDir $blocked)) {
            $violations.Add("금지된 루트 파일: $blocked")
        }
    }

    foreach ($alias in Get-CommercialBlockedEngineAliases) {
        if (Test-Path (Join-Path $LayoutDir "engine\$alias")) {
            $violations.Add("금지된 엔진 별칭: engine\$alias")
        }
    }

    foreach ($pattern in @('*.msi', '*.pdb', '*.wixpdb', 'SmartPerformanceDoctor.Setup*')) {
        $hits = Get-ChildItem $LayoutDir -Filter $pattern -File -Recurse -ErrorAction SilentlyContinue
        foreach ($hit in $hits) {
            $rel = $hit.FullName.Substring($LayoutDir.Length + 1)
            $violations.Add("금지된 설치 산출물: $rel")
        }
    }

    foreach ($dirName in Get-CommercialBlockedDirectoryNames) {
        if (Test-Path (Join-Path $LayoutDir $dirName)) {
            $violations.Add("금지된 디렉터리: $dirName\")
        }
    }

    if (-not (Test-Path (Join-Path $LayoutDir 'PCCare.exe'))) {
        $violations.Add('필수 누락: PCCare.exe')
    }

    if (-not (Test-Path (Join-Path $LayoutDir 'FOLDER_LAYOUT.txt'))) {
        $violations.Add('필수 누락: FOLDER_LAYOUT.txt')
    }

    return $violations
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