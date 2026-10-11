param([Parameter(Mandatory=$true)][string]$Target)
$ErrorActionPreference='Stop'
function Get-Sha256([string]$Path) { $sha=[Security.Cryptography.SHA256]::Create(); try { return ([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Path))))).Replace('-','') } finally { $sha.Dispose() } }
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../'))
$allowed=[IO.Path]::GetFullPath((Join-Path $root 'artifacts/wo11/rollback-copy.cs'))
if([IO.Path]::GetFullPath($Target) -ne $allowed){throw 'IndependentCopyRequired'}
if((Get-Sha256 $Target) -ne '2720EEDBA9BD4C8A8E8D571276D607EB514381FDA93155456E4A4317AC3486FC'){throw 'ModifiedCopyHashMismatch'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ORIGINAL_FILE.cs') -Destination $Target
if((Get-Sha256 $Target) -ne 'D28B3C9388866B178019A17399FB40E2579ECE3CEAE314611FD7F52BFDCA8A2E'){throw 'RestoreHashMismatch'}
Write-Output 'SOURCE_COPY_RESTORED originalHashMatch=True'
