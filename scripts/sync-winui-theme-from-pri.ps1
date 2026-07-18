param(
    [string]$PublishDir = "",
    [string]$MakePri = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.28000.0\x64\makepri.exe"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")

if (-not $PublishDir) {
    $PublishDir = Get-AppPublishOutput -ProjectRoot $ProjectRoot
}

$pri = Join-Path $PublishDir "Microsoft.UI.Xaml.pri"
if (-not (Test-Path -LiteralPath $pri)) {
    $controlsPri = Join-Path $PublishDir "Microsoft.UI.Xaml.Controls.pri"
    if (Test-Path -LiteralPath $controlsPri) {
        Copy-Item $controlsPri $pri -Force
    }
}

if (-not (Test-Path -LiteralPath $pri)) {
    throw "Microsoft.UI.Xaml.pri not found under: $PublishDir"
}

if (-not (Test-Path -LiteralPath $MakePri)) {
    $MakePri = (Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter "makepri.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\makepri\.exe$" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1).FullName
}

if (-not $MakePri) {
    throw "makepri.exe not found. Install Windows SDK 10."
}

$dumpXml = Join-Path $env:TEMP ("pccare-mui-pri-{0}.xml" -f ([guid]::NewGuid().ToString("N")))
& $MakePri dump /if $pri /of $dumpXml /dt Detailed /o | Out-Null
if (-not (Test-Path -LiteralPath $dumpXml)) {
    throw "makepri dump failed: $pri"
}

$xml = Get-Content $dumpXml -Raw
$destDir = Join-Path $ProjectRoot "content\winui\Microsoft.UI.Xaml\Themes"
New-Item -ItemType Directory -Force -Path $destDir | Out-Null

function Export-PriXbf {
    param([string]$Name)
    $pattern = "(?s)<NamedResource name=`"$Name`"[^>]*>.*?<Base64Value>(.*?)</Base64Value>"
    if ($xml -notmatch $pattern) {
        throw "PRI dump missing embedded resource: $Name"
    }

    $bytes = [Convert]::FromBase64String($Matches[1])
    $out = Join-Path $destDir $Name
    [IO.File]::WriteAllBytes($out, $bytes)
    Write-Host "[OK] $Name ($($bytes.Length) bytes) -> $out" -ForegroundColor Green
}

Export-PriXbf "themeresources.xbf"
Export-PriXbf "generic.xbf"

$markerXaml = Join-Path $destDir "themeresources.xaml"
@'
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Companion to themeresources.xbf (vendored from Microsoft.UI.Xaml.pri). -->
</ResourceDictionary>
'@ | Set-Content -Path $markerXaml -Encoding UTF8

Remove-Item $dumpXml -Force -ErrorAction SilentlyContinue
Write-Host "[OK] WinUI theme assets refreshed under content\winui" -ForegroundColor Cyan