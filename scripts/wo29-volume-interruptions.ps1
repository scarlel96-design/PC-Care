$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') { throw 'HOSTED_RUNNER_REQUIRED' }
$out = 'artifacts/wo29-stage'
New-Item -ItemType Directory -Force $out | Out-Null
$probe = (Resolve-Path 'tools/SmartPerformanceDoctor.WO27Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO27Probe.exe').Path
$rows = [Collections.Generic.List[object]]::new()
$src = git rev-parse HEAD
try {
 foreach ($mode in 'A','B') {
  foreach ($point in 'BeforeDirectory','AfterDirectory','BeforeCopy','DuringCopy','AfterCopy','BeforeFlush','AfterFlush','BeforePublish','AfterPublish','BeforeJournal','AfterJournal') {
   $root = Join-Path $env:RUNNER_TEMP ('wo27-stage29-'+[guid]::NewGuid().ToString('N'))
   $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
   $acl = [Security.AccessControl.DirectorySecurity]::new()
   $acl.SetAccessRuleProtection($true,$false)
   $acl.SetOwner($sid)
   $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
   [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($root),$acl)
   $verb = "stage29:run:${mode}:$point"
   $child = Start-Process $probe -ArgumentList $verb,('"' + $root + '"') -PassThru -WindowStyle Hidden -RedirectStandardOutput "$root/stdout" -RedirectStandardError "$root/stderr"
   try {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (!(Test-Path "$root/signal") -and !$child.HasExited -and $timer.Elapsed.TotalSeconds -lt 45) { Start-Sleep -Milliseconds 50 }
    if ($child.HasExited -and $child.ExitCode -eq 12) { $rows.Add(@{Status='NOT TESTED';Reason='DISTINCT_VOLUMES_UNAVAILABLE';ExitCode=12});return }
    if ($child.HasExited -or !(Test-Path "$root/signal") -or (Get-Content "$root/signal" -Raw) -cne $point) { throw "Checkpoint missing: $mode/$point; exit=$($child.ExitCode); $(Get-Content "$root/stderr" -Raw)" }
    $child.Kill();$child.WaitForExit();$kill=$child.ExitCode
    if ($kill -eq 0) { throw 'Unexpected worker exit' }
   } finally { if (!$child.HasExited) { $child.Kill();$child.WaitForExit() };$child.Dispose() }
   $inspect="stage29:inspect:${mode}:$point"
   $raw=& $probe $inspect $root
   if($LASTEXITCODE -ne 0){throw 'First inspection failed'}
   $again=& $probe $inspect $root
   if($LASTEXITCODE -ne 0 -or $raw -cne $again){throw 'Unstable inspection'}
   $r=$raw|ConvertFrom-Json
   $events=@(Get-Content "$root/stdout" | ForEach-Object { $_ | ConvertFrom-Json })
   $source=@($events|Where-Object Stage -eq Input)[0]
   $prepared=@($events|Where-Object Stage -eq Prepared)[0]
   $hasPeer=$point -ne 'BeforeDirectory'
   $hasInput=$point -notin 'BeforeDirectory','AfterDirectory'
   $hasTemp=$point -in 'DuringCopy','AfterCopy','BeforeFlush','AfterFlush','BeforePublish'
   $published=$point -in 'AfterPublish','BeforeJournal','AfterJournal'
   if (($r.State -eq 'Inspected') -ne $hasPeer -or [bool]$r.Source -ne $hasInput -or [bool]$r.Temp -ne $hasTemp -or [bool]$r.Published -ne $published -or [bool]$r.Receipt -ne ($point -eq 'AfterJournal')) { throw "Unexpected inventory: $mode/$point" }
   if ($hasInput -and ($r.Source.Hash -ne $source.Source.Hash -or $r.Source.Id -ne $source.Source.Id -or $r.Source.Size -ne 1048576)) { throw 'Source mismatch' }
   if ($hasTemp -and ($point -eq 'DuringCopy' -and $r.Temp.Size -ne 4096 -or $point -ne 'DuringCopy' -and $r.Temp.Hash -ne $r.Source.Hash)) { throw 'Staging mismatch' }
   if ($published -and ($r.Published.Hash -ne $r.Source.Hash -or $r.Published.Id -ne $prepared.Id)) { throw 'Publication mismatch' }
   $rows.Add(@{Mode=$mode;Checkpoint=$point;Command="WO27Probe $verb <fixture>; WO27Probe $inspect <fixture> twice";KilledExit=$kill;InspectionExits=@(0,0);State=$r.State;SourceHash=$r.Source.Hash;StagedBytes=$r.Temp.Size;PublishedHash=$r.Published.Hash;ReceiptVerified=[bool]$r.Receipt;IdenticalReads=$true;Status='PASS'})
  }
 }
} finally {
 @{Source=$src;Command='pwsh -File scripts/wo29-volume-interruptions.ps1';Environment='GitHub-hosted Windows; private synthetic directories';ProbeSha256=(Get-FileHash $probe).Hash;Rows=$rows}|ConvertTo-Json -Depth 6|Set-Content "$out/staging.json"
}
