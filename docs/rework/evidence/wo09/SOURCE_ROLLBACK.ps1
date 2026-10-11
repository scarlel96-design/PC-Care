param([Parameter(Mandatory=$true)][string]$TargetRoot)
$ErrorActionPreference='Stop'
function Read-Hash([string]$Path) {
 $stream=[IO.File]::OpenRead($Path)
 $algorithm=[Security.Cryptography.SHA256]::Create()
 try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','') } finally {$algorithm.Dispose();$stream.Dispose()}
}
$root=[IO.Path]::GetFullPath($TargetRoot)
if([IO.Path]::GetFileName($root) -ne 'rollback-copy'){throw 'Only isolated rollback-copy is supported'}
$target=Join-Path $root 'ReferenceComparison.cs'
$modified=Join-Path $PSScriptRoot 'MODIFIED_FILE.cs'
$original=Join-Path $PSScriptRoot 'ORIGINAL_FILE.cs'
if((Read-Hash $target) -ne (Read-Hash $modified)){throw 'Modified copy hash mismatch'}
[IO.File]::Copy($original,$target,$true)
if((Read-Hash $target) -ne (Read-Hash $original)){throw 'Restored copy hash mismatch'}
Write-Output 'SOURCE_COPY_RESTORED;INSTALL_TRANSACTION_NOT_TESTED'
