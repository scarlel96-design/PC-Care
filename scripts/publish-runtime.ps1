param(
    [string]$AppOut = "",
    [string]$RuntimeDir = "",
    [switch]$SkipProcessStop
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
$OutputEncoding = [System.Text.UTF8Encoding]::new()
chcp 65001 | Out-Null
$env:DOTNET_CLI_UI_LANGUAGE = "ko"

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")

if (-not $AppOut) {
    $AppOut = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0"
}
if (-not $RuntimeDir) {
    $RuntimeDir = Get-RuntimeRoot -ProjectRoot $ProjectRoot
}

if (-not (Test-Path $AppOut)) {
    throw "빌드 출력이 없습니다. scripts\build.ps1 를 먼저 실행하세요: $AppOut"
}

function Stop-AppProcesses {
    foreach ($serviceName in @("AstraCareAegisRecovery", "PCCareAegisRecovery")) {
        $svc = Get-Service $serviceName -ErrorAction SilentlyContinue
        if (-not $svc) { continue }
        if ($svc.Status -ne 'Stopped') {
            Write-Host "[INFO] 서비스 중지: $serviceName" -ForegroundColor Yellow
            try { Stop-Service $serviceName -Force -ErrorAction Stop } catch { & sc.exe stop $serviceName | Out-Null }
        }
        for ($i = 0; $i -lt 10; $i++) {
            $svc.Refresh()
            if ($svc.Status -eq 'Stopped') { break }
            Start-Sleep -Seconds 1
        }
    }

    foreach ($name in @("SmartPerformanceDoctor", "AstraCare", "PCCare", "AegisRecoveryService", "AegisRecoveryHelper")) {
        Get-Process $name -ErrorAction SilentlyContinue | ForEach-Object {
            Write-Host "[INFO] 종료: $name (PID $($_.Id))" -ForegroundColor Yellow
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    }
    Start-Sleep -Seconds 3
}

function Test-PortableExecutable {
    param([string]$Path, [long]$MinBytes = 65536)
    if (-not (Test-Path $Path)) { return $false }
    $info = Get-Item $Path
    if ($info.Length -lt $MinBytes) { return $false }
    $bytes = [byte[]]::new(2)
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $read = $stream.Read($bytes, 0, 2)
        return ($read -eq 2 -and $bytes[0] -eq 0x4D -and $bytes[1] -eq 0x5A)
    }
    finally {
        $stream.Dispose()
    }
}

function Copy-RuntimeFileWithRetry {
    param([string]$SourceFile, [string]$DestFile, [int]$MaxAttempts = 6)
    $destDir = Split-Path $DestFile -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            Copy-Item $SourceFile $DestFile -Force
            return
        }
        catch {
            if ($attempt -eq $MaxAttempts) {
                $relative = $DestFile
                if ($DestFile -match '[\\/]engine[\\/]') {
                    Write-Host "[WARN] 잠긴 engine 파일 건너뜀 (복구 서비스 실행 중): $relative" -ForegroundColor Yellow
                    return
                }
                throw
            }
            Start-Sleep -Seconds 2
        }
    }
}

function Copy-RuntimeFiles {
    param([string]$Source, [string]$Destination, [switch]$SkipContentTree)
    Get-ChildItem $Source -Recurse -File | Where-Object {
        $_.Extension -ne ".pdb" -and
        (-not $SkipContentTree -or -not ($_.FullName.Substring($Source.Length + 1).StartsWith("content\", [StringComparison]::OrdinalIgnoreCase)))
    } | ForEach-Object {
        $relative = $_.FullName.Substring($Source.Length + 1)
        $dest = Join-Path $Destination $relative
        Copy-RuntimeFileWithRetry -SourceFile $_.FullName -DestFile $dest
    }
}

function Sync-RuntimeUiAssets {
    param([string]$Source, [string]$Destination)
    foreach ($dirName in @("Views", "Controls", "Resources", "Styles")) {
        $srcDir = Join-Path $Source $dirName
        if (-not (Test-Path $srcDir)) {
            throw "UI sync 실패 — 빌드 출력에 $dirName 폴더가 없습니다. dotnet build -c Release -p:Platform=x64 후 다시 시도하세요: $srcDir"
        }
        $destDir = Join-Path $Destination $dirName
        if (Test-Path $destDir) {
            Remove-Item $destDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        Copy-Item $srcDir $destDir -Recurse -Force
        Write-Host "[OK] UI sync: $dirName" -ForegroundColor DarkGray
    }
}

function Assert-RuntimeUiAssetsMatch {
    param([string]$Source, [string]$Destination)
    foreach ($rel in @(
            "Views\UnifiedCarePage.xbf",
            "Views\ProgramProtectionCenterPage.xbf",
            "Resources\MacDesignTokens.xbf"
        )) {
        $srcPath = Join-Path $Source $rel
        $destPath = Join-Path $Destination $rel
        if (-not (Test-Path $srcPath)) {
            throw "UI 검증 실패 — 빌드 출력 누락: $rel"
        }
        if (-not (Test-Path $destPath)) {
            throw "UI 검증 실패 — 런타임 누락: $rel (publish-runtime UI sync 확인)"
        }
        $srcHash = (Get-FileHash -Algorithm SHA256 $srcPath).Hash
        $destHash = (Get-FileHash -Algorithm SHA256 $destPath).Hash
        if ($srcHash -ne $destHash) {
            throw "UI 검증 실패 — $rel 이 빌드 출력과 일치하지 않습니다. publish-runtime을 다시 실행하세요."
        }
    }
    Write-Host "[OK] Runtime UI assets match build output." -ForegroundColor Green
}

function Copy-RuntimeTree {
    param([string]$Source, [string]$Destination)
    $destinationResolved = (Resolve-Path -LiteralPath $Destination -ErrorAction SilentlyContinue)?.Path ?? $Destination
    $projectResolved = (Resolve-Path -LiteralPath $ProjectRoot).Path
    $publishInPlace = $destinationResolved.Equals($projectResolved, [StringComparison]::OrdinalIgnoreCase)

    if ($publishInPlace) {
        Remove-RuntimePublishArtifacts -ProjectRoot $Destination
    }
    elseif (Test-Path $Destination) {
        Remove-Item $Destination -Recurse -Force
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }
    else {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }

    # 프로젝트 루트 인플레이스 배포는 robocopy(자식→부모)에서 멈출 수 있어 명시적 복사를 사용합니다.
    if (-not $publishInPlace) {
        $robocopy = Get-Command robocopy -ErrorAction SilentlyContinue
        if ($robocopy) {
            $null = & robocopy $Source $Destination /E /NFL /NDL /NJH /NJS /NC /NS /NP /XD obj /XF *.pdb /R:1 /W:1 2>&1
            if ($LASTEXITCODE -ge 8) {
                throw "robocopy 실패 (exit $LASTEXITCODE): $Source -> $Destination"
            }
        }
        else {
            Copy-RuntimeFiles -Source $Source -Destination $Destination
        }
    }
    else {
        Copy-RuntimeFiles -Source $Source -Destination $Destination -SkipContentTree:$publishInPlace
    }

    # Aegis 보조 도구는 engine/ 에만 유지
    foreach ($name in @("AegisRecoveryHelper.exe", "AegisRecoveryService.exe")) {
        $rootLeak = Join-Path $Destination $name
        if (Test-Path $rootLeak) {
            Remove-Item $rootLeak -Force
        }
    }
    foreach ($pattern in @("AegisRecovery*.deps.json", "AegisRecovery*.runtimeconfig.json")) {
        Get-ChildItem $Destination -Filter $pattern -File -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

function Move-EngineBinaries {
    param([string]$Root)
    $engineDir = Join-Path $Root "engine"
    New-Item -ItemType Directory -Path $engineDir -Force | Out-Null
    foreach ($name in @("smart_performance_doctor_core.exe", "smart_performance_doctor_repair_helper.exe")) {
        $rootPath = Join-Path $Root $name
        $enginePath = Join-Path $engineDir $name
        if (Test-Path $rootPath) {
            Move-Item $rootPath $enginePath -Force
        }
    }
    foreach ($pair in @(
            @{ Src = "smart_performance_doctor_core.exe"; Alias = "AstraCore.exe" },
            @{ Src = "smart_performance_doctor_repair_helper.exe"; Alias = "AstraRepairHelper.exe" }
        )) {
        $src = Join-Path $engineDir $pair.Src
        $alias = Join-Path $engineDir $pair.Alias
        if ((Test-Path $src) -and -not (Test-Path $alias)) {
            Copy-Item $src $alias -Force
        }
    }
}

function Write-RuntimeExecutable {
    param([string]$Root, [string]$ProjectRoot)
    $mainExe = Join-Path $Root "SmartPerformanceDoctor.exe"
    $preferredExe = Join-Path $Root "PCCare.exe"
    if (-not (Test-PortableExecutable $mainExe)) {
        throw "메인 실행 파일이 유효하지 않습니다: $mainExe"
    }

    Copy-Item $mainExe $preferredExe -Force
    foreach ($pair in @(
            @{ Src = "SmartPerformanceDoctor.deps.json"; Dest = "PCCare.deps.json"; Required = $true },
            @{ Src = "SmartPerformanceDoctor.runtimeconfig.json"; Dest = "PCCare.runtimeconfig.json"; Required = $true },
            @{ Src = "SmartPerformanceDoctor.pri"; Dest = "PCCare.pri"; Required = $false }
        )) {
        $srcPath = Join-Path $Root $pair.Src
        $destPath = Join-Path $Root $pair.Dest
        if (-not (Test-Path $srcPath)) {
            if ($pair.Required) {
                throw "PCCare companion 생성 실패 — 누락: $($pair.Src)"
            }
            continue
        }
        Copy-Item $srcPath $destPath -Force
    }

    foreach ($legacyLauncher in @("AstraCare.exe", "AstraCare_Setup.exe", "start.bat")) {
        $legacyPath = Join-Path $Root $legacyLauncher
        if (Test-Path $legacyPath) {
            Remove-Item $legacyPath -Force
        }
    }

    foreach ($legacyLauncher in @("start.bat", ("{0}{1}.bat" -f [char]0xC2DC, [char]0xC791))) {
        $legacyPath = Join-Path $ProjectRoot $legacyLauncher
        if (Test-Path $legacyPath) {
            Remove-Item $legacyPath -Force
        }
    }
}

function Assert-RuntimeIntegrity {
    param([string]$Root, [string]$SourceRoot)
    $checks = @(
        @{ Path = Join-Path $Root "PCCare.exe"; Min = 65536 },
        @{ Path = Join-Path $Root "PCCare.deps.json"; Min = 256 },
        @{ Path = Join-Path $Root "PCCare.runtimeconfig.json"; Min = 64 },
        @{ Path = Join-Path $Root "SmartPerformanceDoctor.exe"; Min = 65536 },
        @{ Path = Join-Path $Root "SmartPerformanceDoctor.dll"; Min = 65536 },
        @{ Path = Join-Path $Root "Microsoft.WinUI.dll"; Min = 65536 },
        @{ Path = Join-Path $Root "runtimes\win-x64\native\Microsoft.WindowsAppRuntime.Bootstrap.dll"; Min = 4096 }
    )
    foreach ($check in $checks) {
        if (-not (Test-Path $check.Path)) {
            throw "필수 런타임 누락: $($check.Path)"
        }
        if (-not (Test-PortableExecutable $check.Path $check.Min) -and $check.Path -like "*.exe") {
            throw "실행 파일 손상: $($check.Path)"
        }
        if ((Get-Item $check.Path).Length -lt $check.Min) {
            throw "파일 크기 비정상: $($check.Path)"
        }
    }

    $srcExe = Join-Path $SourceRoot "SmartPerformanceDoctor.exe"
    $destExe = Join-Path $Root "SmartPerformanceDoctor.exe"
    $srcLen = (Get-Item $srcExe).Length
    $destLen = (Get-Item $destExe).Length
    if ($destLen -lt $srcLen) {
        throw "배포 후 exe 크기 비정상: source=$srcLen dest=$destLen (dest must be >= source after Authenticode signing)"
    }
}

Write-Host "== Publish runtime -> project root (PCCare.exe) ==" -ForegroundColor Cyan
Write-Host "Source: $AppOut"
Write-Host "Target: $RuntimeDir"

if (-not $SkipProcessStop) {
    Stop-AppProcesses
}

Copy-RuntimeTree -Source $AppOut -Destination $RuntimeDir
Sync-RuntimeUiAssets -Source $AppOut -Destination $RuntimeDir
Move-EngineBinaries -Root $RuntimeDir

function Sync-ProjectContentAssets {
    param([string]$ProjectRoot, [string]$Destination)
    $contentSrc = (Resolve-Path -LiteralPath (Join-Path $ProjectRoot "content") -ErrorAction SilentlyContinue)?.Path
    if (-not $contentSrc) { return }
    $contentDest = Join-Path $Destination "content"
    if ($contentSrc.Equals((Resolve-Path -LiteralPath $contentDest -ErrorAction SilentlyContinue)?.Path, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Host "[OK] content/ authoritative at project root" -ForegroundColor DarkGray
        return
    }
    Copy-Item $contentSrc $contentDest -Recurse -Force
    Write-Host "[OK] content/ synced from project source" -ForegroundColor DarkGray
}

function Ensure-AegisSigningKey {
    param([string]$Root, [string]$ProjectRoot)
    $targetDir = Join-Path $Root "artifacts\signing"
    $targetKey = Join-Path $targetDir "aegis-dev-private.pem"
    if (Test-Path $targetKey) {
        return
    }

    $candidates = @(
        (Join-Path $ProjectRoot "artifacts\signing\aegis-dev-private.pem"),
        (Join-Path $ProjectRoot "tests\SmartPerformanceDoctor.Tests\TestAssets\aegis-test-private.pem")
    )
    foreach ($source in $candidates) {
        if (-not (Test-Path $source)) { continue }
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        Copy-Item $source $targetKey -Force
        Write-Host "[OK] Aegis signing key deployed: $targetKey" -ForegroundColor Green
        return
    }

    Write-Host "[WARN] Aegis signing key not deployed — manifest auto-sign may fail until key is present." -ForegroundColor Yellow
}

Ensure-AegisSigningKey -Root $RuntimeDir -ProjectRoot $ProjectRoot
Sync-ProjectContentAssets -ProjectRoot $ProjectRoot -Destination $RuntimeDir
& (Join-Path $PSScriptRoot "trim-runtime-rid.ps1") -RuntimeDir $RuntimeDir
Write-RuntimeExecutable -Root $RuntimeDir -ProjectRoot $ProjectRoot
& (Join-Path $PSScriptRoot "sign-runtime-payload.ps1") -PayloadDir $RuntimeDir -SkipIfNoCert
Sync-RuntimeUiAssets -Source $AppOut -Destination $RuntimeDir
Write-RuntimeExecutable -Root $RuntimeDir -ProjectRoot $ProjectRoot
Assert-RuntimeUiAssetsMatch -Source $AppOut -Destination $RuntimeDir
Assert-RuntimeIntegrity -Root $RuntimeDir -SourceRoot $AppOut

$readme = @(
    "PC 케어 프로",
    "",
    "Run: PCCare.exe",
    "",
    "Layout:",
    "  PCCare.exe - main app (user entry)",
    "  SmartPerformanceDoctor.exe - runtime host",
    "  engine/ - core, repair, Aegis helpers",
    "  content/ - rules, assets, commercial packs",
    "  runtimes/ - Windows App SDK",
    "",
    "Published: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
) -join "`r`n"
Set-Content (Join-Path $RuntimeDir "README.txt") $readme -Encoding UTF8

Write-Host "[OK] Runtime published: $RuntimeDir" -ForegroundColor Green
Write-Host "[OK] Main EXE: $((Get-Item (Join-Path $RuntimeDir 'SmartPerformanceDoctor.exe')).Length) bytes" -ForegroundColor Green
Write-Host "[OK] User EXE: $(Join-Path $RuntimeDir 'PCCare.exe')" -ForegroundColor Green
Remove-LegacyRuntimeDist -ProjectRoot $ProjectRoot