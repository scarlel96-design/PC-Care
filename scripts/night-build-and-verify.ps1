param(
    [string]$Version = "50.1.1"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

Write-Host "== Night build + deploy + launch verify ==" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "build-app.ps1")

$publish = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish"
$buildOut = Join-Path $ProjectRoot "src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64"
if (-not (Test-Path (Join-Path $publish "App.xbf"))) {
    foreach ($dirName in @("Views", "Controls", "Resources", "Styles")) {
        $srcDir = Join-Path $buildOut $dirName
        if (Test-Path $srcDir) {
            $destDir = Join-Path $publish $dirName
            if (Test-Path $destDir) { Remove-Item $destDir -Recurse -Force -ErrorAction SilentlyContinue }
            Copy-Item $srcDir $destDir -Recurse -Force
        }
    }
    foreach ($shellXbf in @("App.xbf", "MainWindow.xbf")) {
        $srcXbf = Join-Path $buildOut $shellXbf
        if (Test-Path $srcXbf) {
            Copy-Item $srcXbf (Join-Path $publish $shellXbf) -Force
        }
    }
}
if (-not (Test-Path (Join-Path $publish "Microsoft.UI.Xaml.pri"))) {
    $controls = Join-Path $publish "Microsoft.UI.Xaml.Controls.pri"
    if (Test-Path $controls) {
        Copy-Item $controls (Join-Path $publish "Microsoft.UI.Xaml.pri") -Force
    }
}

& (Join-Path $PSScriptRoot "verify-publish-no-uac.ps1") -PublishDir $publish -WaitSeconds 20
if ($LASTEXITCODE -ne 0) {
    throw "Publish verify (no UAC) failed."
}

# Program Files deploy + elevated launch require interactive UAC — run manually when needed.

& (Join-Path $PSScriptRoot "build-modular-setup.ps1") -Version $Version -SkipAppBuild

$setupSrc = Join-Path $ProjectRoot "artifacts\installer\setup\PCCare_Setup_v$Version.exe"
$releaseBase = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)) "코딩 작업"
$releaseDir = Join-Path $releaseBase "PCCare_Release_v$Version"
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
Copy-Item $setupSrc (Join-Path $releaseDir "PCCare_Setup_v$Version.exe") -Force

$hash = (Get-FileHash (Join-Path $releaseDir "PCCare_Setup_v$Version.exe") -Algorithm SHA256).Hash.ToLower()
Set-Content (Join-Path $releaseDir "SHA256SUMS.txt") "$hash  PCCare_Setup_v$Version.exe" -Encoding UTF8

Write-Host "[OK] Night pipeline complete. Setup SHA256: $hash" -ForegroundColor Green