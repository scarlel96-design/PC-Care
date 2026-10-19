param([string]$OutputDirectory='artifacts/wo27-midcopy',[int[]]$Sizes=@(4096,1048576,16777216,67108864),[switch]$Quick)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$out=Join-Path $repo $OutputDirectory;New-Item -ItemType Directory -Force $out|Out-Null
if(!$env:RUNNER_TEMP){throw 'RUNNER_TEMP_REQUIRED'}
$probe=Join-Path $repo 'tools/SmartPerformanceDoctor.WO27Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO27Probe.exe'
$source=(& git -C $repo rev-parse HEAD).Trim();$records=[Collections.Generic.List[object]]::new()
function Run([string]$Verb,[string]$Root){
 $raw=& $probe $Verb $Root;$code=$LASTEXITCODE
 if($code -notin 0,10){throw "PROBE_FAILED:$Verb/$code"}
 @{Command="WO27Probe $Verb <SYNTHETIC_ROOT>";ExitCode=$code;Output=($raw -join "`n")}
}
function KillAt([string]$Verb,[string]$Root){
 $signal=Join-Path $Root 'ready.signal';if(Test-Path -LiteralPath $signal){Remove-Item -LiteralPath $signal}
 $start=[Diagnostics.ProcessStartInfo]::new($probe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $start.ArgumentList.Add($Verb);$start.ArgumentList.Add($Root)
 $child=[Diagnostics.Process]::Start($start);$watch=[Diagnostics.Stopwatch]::StartNew()
 try{
  while(!(Test-Path -LiteralPath $signal) -and !$child.HasExited -and $watch.Elapsed.TotalSeconds -lt 60){Start-Sleep -Milliseconds 20}
  if(!(Test-Path -LiteralPath $signal)){throw "CHECKPOINT_NOT_REACHED:$Verb/$($child.StandardError.ReadToEnd())"}
  $child.Kill($true);$child.WaitForExit()
  @{Command="WO27Probe $Verb <SYNTHETIC_ROOT>; parent Process.Kill(true)";ExitCode=$child.ExitCode;Output=$child.StandardOutput.ReadToEnd();KilledByParent=$true}
 }finally{if(!$child.HasExited){$child.Kill($true);$child.WaitForExit()};$child.Dispose()}
}
try{
 foreach($size in $Sizes){
  $points=@('BeforeFirstWrite','FirstBlock','4096')
  if($size -ge 1048576){$points+='1048576'}
  $points+=@([string]($size/4),[string]($size/2),[string](3*$size/4),'BeforeLastWrite','AfterLastWrite','BeforeFlush','AfterFlush','AfterReturn')
  if($Quick){$points=@('BeforeFirstWrite',[string]($size/2),'AfterFlush')}
  foreach($point in $points){
   $root=Join-Path $env:RUNNER_TEMP ('wo27-copy-'+[guid]::NewGuid().ToString('N'))
   $kind=if($records.Count%2){'repeat'}else{'random'}
   $init=Run "init:${size}:$kind" $root
   if($init.ExitCode -ne 0){throw 'INIT_FAILED'}
   $prepared=$init.Output|ConvertFrom-Json
   $apply=KillAt 'kill:AfterFirstReplacement' $root
   $kill=KillAt "copy:$point" $root
   $progress=Get-Content (Join-Path $root 'copy-checkpoint.json') -Raw|ConvertFrom-Json
   $inspection=Run 'inspect' $root;$state=$inspection.Output|ConvertFrom-Json
   $partial=$state.Current.Hash -ne $prepared.OriginalHash -and $state.Current.Hash -ne $prepared.UpdatedHash
   $classification=if($partial -and $progress.BytesWritten -gt 0 -and $progress.BytesWritten -lt $size){'PartialContentObservedByParent'}elseif($partial){'UnknownContent'}elseif($state.Current.Hash -eq $prepared.OriginalHash){'OriginalVerified'}else{'UpdatedVerified'}
   if($state.Current.Identity -ne $prepared.Identity -or $state.Backup.Hash -ne $prepared.OriginalHash -or $state.Current.AclHash -ne $prepared.AclHash){throw 'IDENTITY_OR_BACKUP_CHANGED'}
   $resume=Run 'resume' $root
   $after=Run 'inspect' $root;$verified=$after.Output|ConvertFrom-Json
   if($partial){
    if($resume.ExitCode -ne 10 -or $state.Reconciliation.CanRequestRecovery -or $verified.Current.Hash -ne $state.Current.Hash -or $verified.Journal.Hash -ne $state.Journal.Hash){throw 'UNKNOWN_CONTENT_CHANGED'}
   }elseif($resume.ExitCode -ne 0 -or $verified.Current.Hash -ne $prepared.OriginalHash){throw 'VERIFIED_RECOVERY_FAILED'}
   if($verified.Current.Identity -ne $prepared.Identity -or $verified.Current.AclHash -ne $prepared.AclHash -or $verified.Backup.Hash -ne $prepared.OriginalHash){throw 'POST_RESUME_IDENTITY_OR_BACKUP_CHANGED'}
   $records.Add(@{Source=$source;Environment='Windows synthetic filesystem; strictAcl=false; parent kill, separate reader and resume processes';Input=@{Size=$size;DataKind=$kind;Point=$point};Setup=$init;ApplyTermination=$apply;CopyTermination=$kill;Progress=$progress;Inspection=$inspection;Classification=$classification;Resume=$resume;After=$after;Status='PASS';PhysicalPowerLoss='NOT TESTED'})
  }
 }
 'WO27_MIDCOPY_MATRIX_PASS='+$records.Count
}finally{
 $records|ConvertTo-Json -Depth 20|Set-Content (Join-Path $out 'midcopy.json')
}
