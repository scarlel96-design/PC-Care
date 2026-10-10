param([Parameter(Mandatory=$true)][string]$CopyPath)
$ErrorActionPreference = 'Stop'
function FileHash([string]$Path) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash([IO.File]::ReadAllBytes($Path))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}
$expected = Join-Path $PSScriptRoot 'ROLLBACK_COPY.props'
if ([IO.Path]::GetFullPath($CopyPath) -ne [IO.Path]::GetFullPath($expected)) { throw 'Use only the independent rollback fixture.' }
$hashes = Get-Content (Join-Path $PSScriptRoot 'SOURCE_HASHES.json') -Raw | ConvertFrom-Json
if ((FileHash $expected) -ne $hashes.modifiedSha256) { throw 'Unexpected copy hash.' }
$original = Join-Path $PSScriptRoot 'ORIGINAL_FILE.props'
if ((FileHash $original) -ne $hashes.originalSha256) { throw 'Original hash mismatch.' }
[IO.File]::Copy($original, $expected, $true)
if ((FileHash $expected) -ne $hashes.originalSha256) { throw 'Restored hash mismatch.' }
Write-Output 'ROLLBACK: original SHA-256 restored on independent source copy'
