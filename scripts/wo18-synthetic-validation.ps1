param(
    [Parameter(Mandatory=$true)][string]$FixtureParent,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$parent=[IO.Path]::GetFullPath($FixtureParent)
$output=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $parent,$output | Out-Null
$probe=Join-Path $PSScriptRoot '../tools/SmartPerformanceDoctor.DeleteProbe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.DeleteProbe.exe'
$root=(& $probe create $parent | Select-Object -Last 1).Trim()
if($LASTEXITCODE -ne 0){throw 'Synthetic fixture creation failed.'}
if(-not $root.StartsWith($parent.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture escaped parent.'}
foreach($mode in @('execute','binding','reconcile')){
    $raw=& $probe $mode $root
    $code=$LASTEXITCODE
    if($code -ne 0){throw "Probe failed: $mode / $code"}
    $record=$raw | ConvertFrom-Json
    if(-not $record.OtherContentMatches){throw 'Unrelated bytes changed.'}
    switch($mode){
        'execute' {if($record.Result.Status -ne 'Completed' -or $record.Result.RequiresManualReview){throw 'Deletion did not complete.'}}
        'binding' {if(-not($record.Result.Authenticated -and $record.Result.ContentFingerprintMatches -and $record.Result.SelectedFileIdMatches -and $record.Result.OwnerBound)){throw 'Final handle binding failed.'}}
        'reconcile' {if($record.Result.Status -ne 'DeletionConfirmedByAvailableEvidence' -or $record.Result.RequiresManualReview){throw 'Completed evidence did not reconcile.'}}
    }
    $raw | Set-Content -LiteralPath (Join-Path $output "$mode.json")
}
$quarantine=& $probe quarantine $parent
if($LASTEXITCODE -ne 0){throw 'Quarantine roundtrip failed.'}
$q=$quarantine | ConvertFrom-Json
if($q.Quarantine.BeforeHash -ne $q.Quarantine.AfterHash -or -not $q.RestoreCollision.Blocked -or -not $q.RestoreCollision.ExistingBytesPreserved -or -not $q.RestoreCollision.QuarantineRetained){throw 'Quarantine evidence mismatch.'}
$quarantine | Set-Content -LiteralPath (Join-Path $output 'quarantine.json')
[ordered]@{SourceCommit=(& git rev-parse HEAD);OS=[Environment]::OSVersion.VersionString;Command='pwsh -NoProfile -File scripts/wo18-synthetic-validation.ps1 -FixtureParent <EXPLICIT_SYNTHETIC_PARENT> -OutputDirectory <EVIDENCE>';Input='Generated 1MiB deletion fixture; generated 1MiB selected quarantine fixture';ExitCode=0;Result='WO18_SYNTHETIC_PASS binding=1 reconciliation=1 quarantine=1 collisionPreserved=1';ProductionFolderDeleteEnabled=$false;ProductionCleanupEnabled=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'execution.json')
Write-Output 'WO18_SYNTHETIC_PASS binding=1 reconciliation=1 quarantine=1 collisionPreserved=1'
