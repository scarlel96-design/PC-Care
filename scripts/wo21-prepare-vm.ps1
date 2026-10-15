param([Parameter(Mandatory=$true)][string]$VMName,[Parameter(Mandatory=$true)][string]$GeneralizedVhd,
 [Parameter(Mandatory=$true)][string]$TestRoot,[switch]$Execute)
$ErrorActionPreference='Stop'
if(![IO.Path]::IsPathFullyQualified($GeneralizedVhd) -or !(Test-Path -LiteralPath $GeneralizedVhd -PathType Leaf)){throw 'GENERALIZED_VHD_REQUIRED'}
if(![IO.Path]::IsPathFullyQualified($TestRoot) -or !(Split-Path $TestRoot -Leaf).StartsWith('wo21-')){throw 'DEDICATED_TEST_ROOT_REQUIRED'}
if(Test-Path -LiteralPath $TestRoot){throw 'NEW_TEST_ROOT_REQUIRED'}
if(!$Execute){'PREPARED: copy generalized VHD; create isolated Generation 2 VM; create and verify checkpoint; no host change executed';return}
if(!(Get-Command New-VM -ErrorAction SilentlyContinue)){throw 'VM_UNAVAILABLE'}
if(Get-VM -Name $VMName -ErrorAction SilentlyContinue){throw 'VM_ALREADY_EXISTS'}
New-Item -ItemType Directory $TestRoot | Out-Null
$copy=Join-Path $TestRoot 'qualification.vhdx'
Copy-Item -LiteralPath $GeneralizedVhd -Destination $copy
New-VM -Name $VMName -Generation 2 -MemoryStartupBytes 8GB -VHDPath $copy -Path $TestRoot | Out-Null
Set-VM -Name $VMName -CheckpointType Standard
Checkpoint-VM -Name $VMName -SnapshotName 'WO21-before-qualification'
$checkpoint=Get-VMSnapshot -VMName $VMName -Name 'WO21-before-qualification'
if(!$checkpoint){throw 'SNAPSHOT_NOT_VERIFIED'}
$checkpoint|Select-Object VMName,Name,Id,CreationTime|ConvertTo-Json|Set-Content (Join-Path $TestRoot 'checkpoint.json')
'VM_PREPARED: no virtual network switch attached; guest accounts and installation remain separate manual qualification steps'
