param([string]$OutputDirectory = 'artifacts/wo08-measurement', [switch]$RunControlledWorkloads, [switch]$IsolatedRunnerReference)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $root -Force | Out-Null
$probe = [IO.Path]::GetFullPath('tools/SmartPerformanceDoctor.DiagnosticsProbe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.DiagnosticsProbe.exe')
foreach ($profile in @('General', 'LowOverhead', 'Detailed')) {
    & $probe 15 (Join-Path $root "$profile.json") $profile
    if ($LASTEXITCODE -ne 0) { throw "Measurement failed: $profile" }
    $m = Get-Content (Join-Path $root "$profile.json") -Raw | ConvertFrom-Json
    $s = $m.snapshot.EtwStatistics
    if ($s.EventsAccepted -ne ($s.EventsProcessed + $s.EventsPendingAtStop)) { throw 'Queue conservation failed' }
    if ($m.environment.elevated -and $s.SessionCleanupStatus -ne 'VerifiedAbsent') { throw 'ETW cleanup failed' }
}
if ($RunControlledWorkloads) {
    foreach ($mode in @('cpu','peak','memory','sequential-read','sequential-write','random-read','random-write','mixed')) {
        $dir = Join-Path $root $mode
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        $threads = [Math]::Min(4,[Environment]::ProcessorCount)
        $process = Start-Process -FilePath $probe -ArgumentList @('--workload', $mode, '12', ('"' + $dir + '"'), $threads) -WindowStyle Hidden -PassThru
        & $probe 12 (Join-Path $dir 'measurement.json') General
        $measurementExit = $LASTEXITCODE
        if (-not $process.WaitForExit(20000)) { throw 'Bounded workload exceeded its deadline' }
        if ($process.ExitCode -ne 0 -or $measurementExit -ne 0) { throw "Workload failed: $mode" }
    }
}
if ($IsolatedRunnerReference) {
    if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Raw ETL reference capture requires an isolated CI runner' }
    $wpr = Get-Command wpr.exe -ErrorAction SilentlyContinue
    if (-not $wpr) {
        @{Status='BLOCKED'; Reason='WPR unavailable'; Accuracy='NOT TESTED'} | ConvertTo-Json | Set-Content (Join-Path $root 'reference.json')
    } else {
        $status = & $wpr.Source -status 2>&1 | Out-String
        if ($status -notmatch 'not recording') { throw 'Existing WPR recording must remain unchanged' }
        $etl = Join-Path $root ('reference-' + [guid]::NewGuid().ToString('N') + '.etl')
        $startUtc = [DateTimeOffset]::UtcNow
        $startOutput = & $wpr.Source -start CPU -filemode 2>&1 | Out-String
        $startExit = $LASTEXITCODE
        $reference = @{Status='BLOCKED'; StartExit=$startExit; StartUtc=$startUtc; Accuracy='NOT TESTED'; StartOutput=$startOutput.Trim()}
        if ($startExit -eq 0) {
            try {
                $dir = Join-Path $root 'reference-workload'
                New-Item -ItemType Directory -Path $dir -Force | Out-Null
                $process = Start-Process -FilePath $probe -ArgumentList @('--workload','cpu','12',('"'+$dir+'"'),[Math]::Min(4,[Environment]::ProcessorCount)) -WindowStyle Hidden -PassThru
                & $probe 12 (Join-Path $dir 'measurement.json') General
                if ($LASTEXITCODE -ne 0) { throw 'Reference observation failed' }
                if (-not $process.WaitForExit(20000) -or $process.ExitCode -ne 0) { throw 'Reference workload failed' }
            } finally {
                $stopOutput = & $wpr.Source -stop $etl 2>&1 | Out-String
                $reference.StopExit = $LASTEXITCODE
                $reference.EndUtc = [DateTimeOffset]::UtcNow
                $reference.StopOutput = $stopOutput.Trim()
            }
            if ($reference.StopExit -eq 0 -and (Test-Path -LiteralPath $etl)) {
                $reference.EtlSha256 = (Get-FileHash -LiteralPath $etl -Algorithm SHA256).Hash
                $reference.EtlBytes = (Get-Item -LiteralPath $etl).Length
                $reference.Status = 'PARTIAL'
                $reference.Reason = 'Independent WPR capture succeeded. Independent WPA interpretation and aligned category ground truth remain unverified.'
                # Remove only the generated reference trace after its hash is recorded.
                if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($etl)) -ne $root) { throw 'Trace path escaped the output directory' }
                Remove-Item -LiteralPath $etl
            }
        }
        $reference | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'reference.json')
    }
}
Write-Output 'WO08_MEASUREMENT_COMPLETED'
