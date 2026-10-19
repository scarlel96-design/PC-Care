param([string]$OutputDirectory='artifacts/wo27-default-copy')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent;$out=Join-Path $repo $OutputDirectory
New-Item -ItemType Directory -Force $out|Out-Null
if(!$env:RUNNER_TEMP){throw 'RUNNER_TEMP_REQUIRED'}
$probe=Join-Path $repo 'tools/SmartPerformanceDoctor.WO27Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO27Probe.exe'
$source=(& git -C $repo rev-parse HEAD).Trim();$rows=[Collections.Generic.List[object]]::new();$size=67108864
function Run([string]$Verb,[string]$Root){$raw=& $probe $Verb $Root;$code=$LASTEXITCODE;if($code -notin 0,10){throw "PROBE_FAILED:$Verb/$code"};@{Command="WO27Probe $Verb <SYNTHETIC_ROOT>";Output=($raw -join "`n");ExitCode=$code}}
function StartWorker([string]$Verb,[string]$Root){
 $start=[Diagnostics.ProcessStartInfo]::new($probe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $start.ArgumentList.Add($Verb);$start.ArgumentList.Add($Root)
 [Diagnostics.Process]::Start($start)
}
function AwaitSignal($Child,[string]$Signal){
 $timer=[Diagnostics.Stopwatch]::StartNew()
 while(!(Test-Path -LiteralPath $Signal) -and !$Child.HasExited -and $timer.Elapsed.TotalSeconds -lt 60){Start-Sleep -Milliseconds 1}
 if(!(Test-Path -LiteralPath $Signal)){if(!$Child.HasExited){$Child.Kill($true);$Child.WaitForExit()};throw ('CHECKPOINT_MISSING:'+ $Child.StandardError.ReadToEnd())}
}
try{
 foreach($threshold in @(1048576,16777216,33554432,50331648)){
  $qualified=$false
  for($attempt=0;$attempt -lt 2 -and !$qualified;$attempt++){
   $root=Join-Path $env:RUNNER_TEMP ('wo27-native-'+[guid]::NewGuid().ToString('N'))
   $init=Run "init:${size}:repeat" $root;if($init.ExitCode -ne 0){throw 'SETUP_FAILED'}
   $prepared=$init.Output|ConvertFrom-Json
   $signal=Join-Path $root 'ready.signal'
   $apply=StartWorker 'kill:AfterFirstReplacement' $root
   try{AwaitSignal $apply $signal;$apply.Kill($true);$apply.WaitForExit();$applyExit=$apply.ExitCode}finally{if(!$apply.HasExited){$apply.Kill($true);$apply.WaitForExit()};$apply.Dispose()}
   Remove-Item -LiteralPath $signal
   $child=StartWorker 'default-copy' $root;$live=$null;$observed=$false;$killed=$false
   try{
    AwaitSignal $child $signal
    $live=[IO.FileStream]::new((Join-Path $root 'install/app.txt'),[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete),1,[IO.FileOptions]::RandomAccess)
    $prefix=[byte[]]::new(4096);$tail=[byte[]]::new(4096)
    [IO.File]::WriteAllText((Join-Path $root 'go.signal'),'GO')
    $timer=[Diagnostics.Stopwatch]::StartNew()
    while(!$child.HasExited -and $timer.Elapsed.TotalSeconds -lt 15){
     $live.Position=$threshold;$live.ReadExactly($prefix,0,4096)
     $live.Position=$size-4096;$live.ReadExactly($tail,0,4096)
     $prefixReady=$prefix[0] -eq 0x41 -and $prefix[4095] -eq 0x41
     $tailUnchanged=$tail[0] -eq 0x72 -and $tail[4095] -eq 0x72
     if($prefixReady -and $tailUnchanged){$observed=$true;$child.Kill($true);$killed=$true;break}
     Start-Sleep -Milliseconds 1
    }
    if(!$child.HasExited){$child.Kill($true);$killed=$true};$child.WaitForExit();$exit=$child.ExitCode
   }finally{if($null -ne $live){$live.Dispose()};if(!$child.HasExited){$child.Kill($true);$child.WaitForExit()};$child.Dispose()}
   $layout=Run 'layout' $root;$shape=$layout.Output|ConvertFrom-Json
   $before=Run 'inspect' $root;$state=$before.Output|ConvertFrom-Json
   $partial=$observed -and $killed -and $shape.LeadingOriginalBytes -gt 0 -and $shape.LeadingOriginalBytes -lt $size -and $shape.TailMatchesUpdatedFixture
   $resume=Run 'resume' $root;$after=Run 'inspect' $root;$end=$after.Output|ConvertFrom-Json
   if($partial){
    if($resume.ExitCode -ne 10 -or $state.Reconciliation.CanRequestRecovery -or $state.Current.Hash -ne $end.Current.Hash -or $state.Journal.Hash -ne $end.Journal.Hash){throw 'PARTIAL_BYTES_CHANGED'}
    if($state.Current.Identity -ne $prepared.Identity -or $end.Current.Identity -ne $prepared.Identity -or $state.Backup.Identity -ne $end.Backup.Identity -or $end.Backup.Hash -ne $prepared.OriginalHash -or $end.Current.AclHash -ne $prepared.AclHash){throw 'IDENTITY_OR_BACKUP_CHANGED'}
    $qualified=$true
   }
   $rows.Add(@{Source=$source;Environment='Windows synthetic filesystem; default FileStream.CopyTo path; no observed-copy hook';Input=@{Size=$size;DataKind='repeat';Threshold=$threshold;Attempt=$attempt};Setup=$init;ApplyTerminationExitCode=$applyExit;CopyTermination=@{Command='WO27Probe default-copy; parent reads completed prefix and unchanged tail, then Process.Kill(true)';ExitCode=$exit;KilledByParent=$killed;ObservedLiveMidCopy=$observed};Layout=$layout;Inspection=$before;Resume=$resume;After=$after;Status=$(if($partial){'PASS'}else{'MISSED_WINDOW_NOT_QUALIFIED'});PhysicalPowerLoss='NOT TESTED'})
  }
  if(!$qualified){throw 'DEFAULT_COPY_WINDOW_NOT_OBSERVED'}
 }
 'WO27_DEFAULT_COPY_NATIVE_PASS='+@($rows|Where-Object Status -eq 'PASS').Count
}finally{$rows|ConvertTo-Json -Depth 20|Set-Content (Join-Path $out 'default-copy.json')}
