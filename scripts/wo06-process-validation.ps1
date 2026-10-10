param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repo = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputDirectory)
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (!$output.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $output)) {
    throw 'Use a new directory under repository artifacts.'
}
New-Item -ItemType Directory -Path $output | Out-Null
$probe = Join-Path $repo 'tests/SmartPerformanceDoctor.UpdateRecoveryProbe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.UpdateRecoveryProbe.exe'
$records = [Collections.Generic.List[object]]::new()
function Invoke-Probe([string]$Verb,[string]$Fixture,[string]$Expected) {
    $lines = @(& $probe $Verb $Fixture 2>&1 | ForEach-Object { $_.ToString() })
    $code = $LASTEXITCODE
    $records.Add([pscustomobject]@{Command="$probe $Verb $Fixture";Input=$Fixture;Output=$lines;ExitCode=$code;Expected=$Expected})
    if (($Expected -eq 'ZERO' -and $code -ne 0) -or ($Expected -eq 'NONZERO' -and $code -eq 0)) {
        throw "Unexpected result for $Verb : $code"
    }
}
try {
    $points = @('AfterNewFileIntent','AfterNewFileCreated','AfterNewFileIdentity','BeforeNewFileRename','AfterNewFileRename',
        'JournalTempCreated','JournalPartialWrite','JournalFlushed','JournalBeforeReplace','JournalAfterReplace','BeforeCommittedJournal')
    foreach ($point in $points) {
        $fixture = Join-Path $output $point
        Invoke-Probe 'prepare' $fixture 'ZERO'
        Invoke-Probe ('kill:'+$point) $fixture 'NONZERO'
        if ($point -eq 'AfterNewFileCreated') {
            Invoke-Probe 'recover' $fixture 'NONZERO'
            Invoke-Probe 'verify-orphan' $fixture 'ZERO'
        } else {
            Invoke-Probe 'recover' $fixture 'ZERO'
            Invoke-Probe 'verify' $fixture 'ZERO'
        }
    }
    $fixture = Join-Path $output 'rollback-reentry'
    Invoke-Probe 'prepare' $fixture 'ZERO'
    Invoke-Probe 'apply-kill' $fixture 'NONZERO'
    Invoke-Probe 'recover-kill' $fixture 'NONZERO'
    Invoke-Probe 'recover' $fixture 'ZERO'
    Invoke-Probe 'verify' $fixture 'ZERO'

    $journalPoints = @('JournalTempCreated','JournalPartialWrite','JournalFlushed','JournalBeforeReplace','JournalAfterReplace')
    foreach ($point in @('AfterRollbackStarted','BeforeRestoredJournal') + $journalPoints) {
        $fixture = Join-Path $output ('recovery-'+$point)
        Invoke-Probe 'prepare' $fixture 'ZERO'
        Invoke-Probe 'apply-kill' $fixture 'NONZERO'
        Invoke-Probe ('recover-kill:'+$point) $fixture 'NONZERO'
        Invoke-Probe 'recover' $fixture 'ZERO'
        Invoke-Probe 'verify' $fixture 'ZERO'
    }
    foreach ($point in $journalPoints) {
        $fixture = Join-Path $output ('restored-journal-'+$point)
        Invoke-Probe 'prepare' $fixture 'ZERO'
        Invoke-Probe 'apply-kill' $fixture 'NONZERO'
        Invoke-Probe ('recover-final-kill:'+$point) $fixture 'NONZERO'
        Invoke-Probe 'recover' $fixture 'ZERO'
        Invoke-Probe 'verify' $fixture 'ZERO'
    }

    $fixture = Join-Path $output 'detached-image'
    Invoke-Probe 'prepare-self' $fixture 'ZERO'
    $holderPath = Join-Path $fixture 'install/engine/SmartPerformanceDoctor.UpdateRecoveryProbe.exe'
    $holder = Start-Process -FilePath $holderPath -ArgumentList @('hold',('"'+$fixture+'"')) -WindowStyle Hidden -PassThru
    try {
        $ready = Join-Path $fixture 'holder.pid'
        for ($index=0; $index -lt 100 -and !(Test-Path -LiteralPath $ready); $index++) { Start-Sleep -Milliseconds 100 }
        if (!(Test-Path -LiteralPath $ready)) { throw 'Synthetic image holder did not start.' }
        Invoke-Probe 'apply' $fixture 'NONZERO'
        if ([IO.File]::ReadAllText((Join-Path $fixture 'install/app.txt')) -ne 'old') { throw 'Locked image changed the installation.' }
    } finally {
        if (!$holder.HasExited) { $holder.Kill(); $holder.WaitForExit() }
        $holder.Dispose()
    }
    Invoke-Probe 'recover' $fixture 'ZERO'
    Invoke-Probe 'verify' $fixture 'ZERO'
    Invoke-Probe 'apply' $fixture 'ZERO'
    Invoke-Probe 'verify-updated' $fixture 'ZERO'

    $fixture = Join-Path $output 'dll-handle-lock'
    Invoke-Probe 'prepare-self' $fixture 'ZERO'
    $locked = [IO.File]::Open((Join-Path $fixture 'install/engine/SmartPerformanceDoctor.UpdateRecoveryProbe.dll'), 'Open','Read','Read')
    try { Invoke-Probe 'apply' $fixture 'NONZERO' } finally { $locked.Dispose() }
    Invoke-Probe 'recover' $fixture 'ZERO'
    Invoke-Probe 'verify' $fixture 'ZERO'
    Invoke-Probe 'apply' $fixture 'ZERO'
    Invoke-Probe 'verify-updated' $fixture 'ZERO'
    'WINDOWS_SYNTHETIC_PROCESS=PASS; VM_E2E=NOT_TESTED; UNKNOWN_ORPHAN=PRESERVED'
} finally {
    $records | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'process-results.json') -Encoding utf8
}
