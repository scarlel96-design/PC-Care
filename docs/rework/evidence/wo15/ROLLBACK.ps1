param([Parameter(Mandatory=$true)][string]$CopyPath)
$ErrorActionPreference='Stop'
function Get-Sha256([string]$Path) {
    $algorithm=[Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($algorithm.ComputeHash([IO.File]::ReadAllBytes($Path)))).Replace('-','') }
    finally { $algorithm.Dispose() }
}
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../../..')).Path
$live=Join-Path $repo 'src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultService.Phase2.cs'
$liveBefore=Get-Sha256 $live
# This checkout uses CRLF. MODIFIED_FILE contains the exact LF Git blob.
if ($liveBefore -ne '50156381D76973AA029866DB3419121CFF4144DDAD9271D92592AA0B8ED48E79') { throw 'Live source mismatch' }
$expected=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts/wo15/rollback-copy/MODIFIED_FILE.copy'))
$actual=[IO.Path]::GetFullPath($CopyPath)
if ($actual -ne $expected) { throw 'Independent source copy required' }
if ((Get-Item -LiteralPath $actual).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse point rejected' }
if ((Get-Sha256 $actual) -ne 'A8442FD4C5839B7B69B844EABC3C3242BB46BCA4283FAD4F8527F388BA5F202A') { throw 'Modified hash mismatch' }
if ((Get-Sha256 (Join-Path $PSScriptRoot 'ORIGINAL_FILE')) -ne '3EC4F8085FEA9938D0F1C65864CF05A9F1E5A7111E0EA02420EFB385102AC984') { throw 'Original hash mismatch' }
$bytes=[IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'ORIGINAL_FILE'))
[IO.File]::WriteAllBytes($actual,$bytes)
if ((Get-Sha256 $actual) -ne '3EC4F8085FEA9938D0F1C65864CF05A9F1E5A7111E0EA02420EFB385102AC984') { throw 'Restored hash mismatch' }
if ((Get-Sha256 $live) -ne $liveBefore) { throw 'Live source changed' }
Write-Output 'SOURCE_COPY_RESTORED=PASS;LIVE_SOURCE_UNCHANGED=PASS;VAULT_RECOVERY=NOT_CLAIMED'
