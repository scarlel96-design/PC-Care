param([Parameter(Mandatory=$true)][string]$Target)
$ErrorActionPreference = 'Stop'
$base = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../artifacts/wo14'))
$resolved = [IO.Path]::GetFullPath($Target)
if (!$resolved.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Independent evidence copy required' }
if ([IO.Path]::GetFileName($resolved) -ne 'rollback-copy.cs.txt') { throw 'Unexpected evidence target' }
function Get-EvidenceHash([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($Path))) }
    finally { $sha.Dispose() }
}
$modified = Join-Path $PSScriptRoot 'MODIFIED_FILE.cs.txt'
$original = Join-Path $PSScriptRoot 'ORIGINAL_FILE.cs.txt'
if ((Get-EvidenceHash $resolved) -ne (Get-EvidenceHash $modified)) { throw 'Modified evidence hash mismatch' }
[IO.File]::WriteAllBytes($resolved, [IO.File]::ReadAllBytes($original))
if ((Get-EvidenceHash $resolved) -ne (Get-EvidenceHash $original)) { throw 'Restoration hash mismatch' }
'SOURCE_COPY_RESTORED=PASS'
