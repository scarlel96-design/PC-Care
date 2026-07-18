$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")

Write-Host "== Build WinUI App (self-contained publish) ==" -ForegroundColor Cyan

$appProj = ".\src\SmartPerformanceDoctor.App\SmartPerformanceDoctor.App.csproj"
$helperProj = ".\src\SmartPerformanceDoctor.AegisRecoveryHelper\SmartPerformanceDoctor.AegisRecoveryHelper.csproj"
$serviceProj = ".\src\SmartPerformanceDoctor.AegisRecoveryService\SmartPerformanceDoctor.AegisRecoveryService.csproj"
$publishRoot = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish"
$engineDir = Join-Path $publishRoot "engine"

$publishArgs = @(
    "-c", "Release",
    "-p:Platform=x64",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:WindowsAppSDKSelfContained=true",
    "-p:PublishSingleFile=false",
    "-p:IncludeNativeLibrariesForSelfExtract=true"
)

dotnet restore .\src\SmartPerformanceDoctor.Contracts\SmartPerformanceDoctor.Contracts.csproj
dotnet restore $appProj
dotnet restore $helperProj
dotnet restore $serviceProj

dotnet build .\src\SmartPerformanceDoctor.Contracts\SmartPerformanceDoctor.Contracts.csproj -c Release
dotnet build $helperProj -c Release -p:Platform=x64
dotnet build $serviceProj -c Release -p:Platform=x64

dotnet publish $appProj @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "WinUI self-contained publish failed."
}

New-Item -ItemType Directory -Path $engineDir -Force | Out-Null
dotnet publish $helperProj @publishArgs -o $engineDir
if ($LASTEXITCODE -ne 0) {
    throw "AegisRecoveryHelper self-contained publish failed."
}

dotnet publish $serviceProj @publishArgs -o $engineDir
if ($LASTEXITCODE -ne 0) {
    throw "AegisRecoveryService self-contained publish failed."
}

foreach ($leak in @(
        "AegisRecoveryHelper.exe", "AegisRecoveryHelper.dll", "AegisRecoveryHelper.deps.json", "AegisRecoveryHelper.runtimeconfig.json",
        "AegisRecoveryService.exe", "AegisRecoveryService.dll", "AegisRecoveryService.deps.json", "AegisRecoveryService.runtimeconfig.json"
    )) {
    $path = Join-Path $publishRoot $leak
    if (Test-Path $path) {
        Remove-Item $path -Force
    }
}

.\scripts\copy-native-engines.ps1

$buildOut = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64"
foreach ($dirName in @("Views", "Controls", "Resources", "Styles")) {
    $srcDir = Join-Path $buildOut $dirName
    if (-not (Test-Path $srcDir)) {
        throw "Publish UI sync failed — build output missing ${dirName}: $srcDir"
    }
    $destDir = Join-Path $publishRoot $dirName
    if (Test-Path $destDir) {
        Remove-Item $destDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Copy-Item $srcDir $destDir -Recurse -Force
}
foreach ($shellXbf in @("App.xbf", "MainWindow.xbf")) {
    $srcXbf = Join-Path $buildOut $shellXbf
    if (-not (Test-Path $srcXbf)) {
        throw "Publish UI sync failed — build output missing ${shellXbf}: $srcXbf"
    }
    Copy-Item $srcXbf (Join-Path $publishRoot $shellXbf) -Force
}

$controlsPri = Join-Path $publishRoot "Microsoft.UI.Xaml.Controls.pri"
$mapPri = Join-Path $publishRoot "Microsoft.UI.Xaml.pri"
if ((Test-Path $controlsPri) -and -not (Test-Path $mapPri)) {
    Copy-Item $controlsPri $mapPri -Force
}

Sync-WinUiThemeAssets -TargetDir $publishRoot -ProjectRoot $ProjectRoot

$mapPri = Join-Path $publishRoot "Microsoft.UI.Xaml.pri"
if (-not (Test-Path $mapPri)) {
    throw "Publish missing WinUI resource map: $mapPri"
}

if (-not (Test-SelfContainedRuntimeLayout $publishRoot)) {
    throw "Self-contained .NET runtime files are missing in publish output: $publishRoot"
}

Sync-PccarePublishBranding -PublishDir $publishRoot

Write-Host "[OK] Self-contained WinUI publish completed: $publishRoot" -ForegroundColor Green