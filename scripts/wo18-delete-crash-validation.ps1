param(
    [Parameter(Mandatory=$true)][string]$FixtureParent,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$parent=[IO.Path]::GetFullPath($FixtureParent)
$output=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $parent,$output | Out-Null
$probe=Join-Path $PSScriptRoot '../tools/SmartPerformanceDoctor.DeleteProbe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.DeleteProbe.exe'
if(-not(Test-Path -LiteralPath $probe)){throw 'Build DeleteProbe before this script.'}
$points=@('BeforeJournalCreate','JournalCreated','HandleOpened','TargetIdentityVerified','BeforeFirstWrite',
    'DuringOverwrite','BeforeFlush','AfterFlush','BeforeVerification','AfterVerification','BeforeDeleteRequest',
    'AfterDeleteRequest','BeforeAudit','AfterAudit','BeforeResultJson','AfterResultJson')
$results=@()
foreach($point in $points){
    $root=(& $probe create $parent | Select-Object -Last 1).Trim()
    if($LASTEXITCODE-ne 0){throw "Fixture creation failed: $point"}
    $resolved=[IO.Path]::GetFullPath($root)
    if(-not $resolved.StartsWith($parent.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture escaped explicit parent.'}
    $stdout=Join-Path $output "$point.stdout.txt"; $stderr=Join-Path $output "$point.stderr.txt"
    $process=Start-Process -FilePath $probe -ArgumentList @('execute',('"'+$root+'"'),$point) -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $deadline=[DateTime]::UtcNow.AddSeconds(120)
    while(-not(Test-Path -LiteralPath (Join-Path $root 'checkpoint.txt'))){
        if($process.HasExited){throw "Worker exited before checkpoint: $point"}
        if([DateTime]::UtcNow-gt $deadline){$process.Kill($true);throw "Checkpoint timeout: $point"}
        Start-Sleep -Milliseconds 100
    }
    $process.Kill($true); $process.WaitForExit(); $termination=$process.ExitCode
    $raw=& $probe reconcile $root
    $reconcileExit=$LASTEXITCODE
    if($reconcileExit-ne 0){throw "Reconciliation failed: $point"}
    $record=$raw | ConvertFrom-Json
    if(-not $record.OtherContentMatches){throw "Unrelated fixture changed: $point"}
    if($point-ne 'BeforeJournalCreate' -and $record.Result.Status-eq 'DeletionConfirmedByAvailableEvidence'){
        throw "Incomplete worker was certified complete: $point"
    }
    $results+=[pscustomobject]@{Checkpoint=$point;Command='DeleteProbe execute <SYNTHETIC_ROOT> '+$point;
        Termination='Separate process killed';WorkerExitCode=$termination;ReconcileCommand='DeleteProbe reconcile <SYNTHETIC_ROOT>';
        ReconcileExitCode=$reconcileExit;Output=$record;Status='PASS'}
    if($point-eq 'AfterResultJson'){
        $marker=Join-Path $root 'checkpoint.txt';Remove-Item -LiteralPath $marker
        $second=Start-Process -FilePath $probe -ArgumentList @('reconcile',('"'+$root+'"'),'ReconciliationRead') -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $output 'ReconciliationRead.stdout.txt') -RedirectStandardError (Join-Path $output 'ReconciliationRead.stderr.txt')
        $deadline=[DateTime]::UtcNow.AddSeconds(30)
        while(-not(Test-Path -LiteralPath $marker)){
            if($second.HasExited){throw 'Reconciliation worker exited before marker.'}
            if([DateTime]::UtcNow-gt $deadline){$second.Kill($true);throw 'Reconciliation checkpoint timeout.'}
            Start-Sleep -Milliseconds 100
        }
        $second.Kill($true);$second.WaitForExit()
        $again=& $probe reconcile $root;$againExit=$LASTEXITCODE;$againRecord=$again|ConvertFrom-Json
        if($againExit-ne 0 -or -not $againRecord.OtherContentMatches){throw 'Reconciliation restart changed unrelated data.'}
        $results+=[pscustomobject]@{Checkpoint='ReconciliationRead';Command='DeleteProbe reconcile <SYNTHETIC_ROOT> ReconciliationRead';
            Termination='Separate reconciliation process killed';WorkerExitCode=$second.ExitCode;ReconcileExitCode=$againExit;Output=$againRecord;Status='PASS'}
    }
}
$results|ConvertTo-Json -Depth 16|Set-Content -LiteralPath (Join-Path $output 'DELETE_CRASH_MATRIX.json') -Encoding utf8
Write-Output "WO18_DELETE_CRASH_PASS checkpoints=$($results.Count) readonlyReconciliation=1 unrelatedBytesPreserved=1"
