param([Parameter(Mandatory=$true)][string]$CopyRoot)
$ErrorActionPreference = 'Stop'
$resolved = (Resolve-Path -LiteralPath $CopyRoot).Path
if ((Split-Path $resolved -Leaf) -ne 'wo12-source-rollback-copy') { throw 'IndependentCopyRequired' }
$file = Join-Path $resolved 'src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultJournal.cs'
if (!(Test-Path -LiteralPath $file)) { throw 'CopySourceMissing' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ORIGINAL_FILE.cs.txt') -Destination $file
Write-Output 'SOURCE_COPY_RESTORED=LabVaultJournal.cs;ACTIVE_BRANCH_UNCHANGED=True'
