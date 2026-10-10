param([Parameter(Mandatory)][string]$CopyPath)
$ErrorActionPreference = 'Stop'
$copy = [IO.Path]::GetFullPath($CopyPath)
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../src/SmartPerformanceDoctor.Diagnostics/EventBuffer.cs'))
if ($copy -eq $source) { throw 'Pass an independent source fixture, not the active source file' }
function Get-SourceHash([string]$Path) {
    $hash = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($hash.ComputeHash([IO.File]::ReadAllBytes($Path))).Replace('-', '') }
    finally { $hash.Dispose() }
}
if (-not [IO.File]::Exists($copy)) { throw 'The independent source fixture must exist' }
$modified = Get-SourceHash ([IO.Path]::Combine($PSScriptRoot, 'MODIFIED_FILE.cs'))
if ((Get-SourceHash $copy) -ne $modified) { throw 'The source fixture does not match the modified artifact' }
[IO.File]::Copy([IO.Path]::Combine($PSScriptRoot, 'ORIGINAL_FILE.cs'), $copy, $true)
$expected = '5B209659D8241C4C90E8B17962E04B51DEDE62518DBFFB5733128E6EF045712C'
if ((Get-SourceHash $copy) -ne $expected) { throw 'Restored source hash mismatch' }
Write-Output ('SOURCE_FIXTURE_RESTORED SHA256=' + $expected)
