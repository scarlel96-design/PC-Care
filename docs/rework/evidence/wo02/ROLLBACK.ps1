param(
    [string]$Target = (Join-Path $PSScriptRoot '..\..\..\..\src\SmartPerformanceDoctor.App\Services\Update\UpdateInstallerService.cs')
)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'UpdateInstallerService.original.cs'
$expectedModified = '216BFBF21087E1C6AB8835AE0940C32241A4987246B55086AD58975E0583A8E9'
$expectedOriginal = '75DD62BA1A2CE07CF0E332BAC60A9D33E5FB6B26884C4E237514CBD0FB982232'
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash -ne $expectedOriginal) { throw 'original hash mismatch' }
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $Target).Hash -ne $expectedModified) { throw 'target hash mismatch' }
Copy-Item -LiteralPath $source -Destination $Target -Force
$restored = (Get-FileHash -Algorithm SHA256 -LiteralPath $Target).Hash
if ($restored -ne $expectedOriginal) { throw 'restore hash mismatch' }
Write-Output "RESTORED SHA256=$restored"
