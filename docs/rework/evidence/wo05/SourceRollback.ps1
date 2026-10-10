param([Parameter(Mandatory)][string]$FixtureDirectory)
$ErrorActionPreference = 'Stop'
function Get-SourceHash([string]$Path) { $sha=[Security.Cryptography.SHA256]::Create(); try { return [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($Path))).Replace('-','') } finally { $sha.Dispose() } }
$fixture = (Resolve-Path -LiteralPath $FixtureDirectory).Path
$repo = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')).TrimEnd('\') + '\'
if (!$fixture.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Source rollback requires an artifacts fixture.' }
if (!(Test-Path -LiteralPath (Join-Path $fixture '.wo05-source-rollback-fixture'))) { throw 'Source fixture marker missing.' }
if ((Get-Item -LiteralPath $fixture).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Source fixture contains a link.' }
$source = Join-Path $PSScriptRoot 'UpdateTransaction.original.cs'
$expected = '3B3E198CE830BA48AB0F11F31F0CCF9F0ACCB44E0D47F8A6527CEF2298532067'
if ((Get-SourceHash $source) -ne $expected) { throw 'Baseline source hash differs.' }
$target = Join-Path $fixture 'UpdateTransaction.cs'
if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Source target contains a link.' }
Copy-Item -LiteralPath $source -Destination $target -Force
if ((Get-SourceHash $target) -ne $expected) { throw 'Source rollback hash differs.' }
'SOURCE_ROLLBACK=PASS; ORIGINAL_SHA256=' + $expected + '; INSTALL_TRANSACTION=NOT_EXECUTED'
