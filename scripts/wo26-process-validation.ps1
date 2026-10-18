param([string]$OutputDirectory='artifacts/wo26-ci')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$out=Join-Path $repo $OutputDirectory
New-Item -ItemType Directory -Force $out|Out-Null
$source=(& git -C $repo rev-parse HEAD).Trim()
$probe=Join-Path $repo 'tools/SmartPerformanceDoctor.WO25Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO25Probe.exe'
if(!$env:RUNNER_TEMP -or ![IO.Path]::IsPathFullyQualified($env:RUNNER_TEMP)){throw 'WO26_RUNNER_TEMP_REQUIRED'}
$records=[Collections.Generic.List[object]]::new()
function Run([string]$Verb,[string]$Root,[int[]]$Expected=@(0)){
 $raw=& $probe $Verb $Root;$code=$LASTEXITCODE
 $records.Add(@{Source=$source;Command='WO25Probe '+$Verb+' <SYNTHETIC_ROOT>';Input=$Verb;Output=($raw -join "`n");ExitCode=$code;Environment='Windows synthetic files; strictAcl=false'})
 if($code -notin $Expected){throw "WO26_CHILD_FAILED:$Verb"}
 return ($raw|ConvertFrom-Json)
}
function Start-Wo26([string]$Verb,[string]$Root){
 $signal=Join-Path $Root 'ready.signal';if(Test-Path -LiteralPath $signal){Remove-Item -LiteralPath $signal}
 $start=[Diagnostics.ProcessStartInfo]::new($probe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $start.ArgumentList.Add($Verb);$start.ArgumentList.Add($Root)
 $child=[Diagnostics.Process]::Start($start);$timer=[Diagnostics.Stopwatch]::StartNew()
 while(!(Test-Path -LiteralPath $signal)){
  if($child.HasExited -or $timer.Elapsed.TotalSeconds -gt 30){if(!$child.HasExited){$child.Kill($true);$child.WaitForExit()};throw ('WO26_CHECKPOINT_MISSING:'+$Verb+':'+$child.StandardError.ReadToEnd())}
  Start-Sleep -Milliseconds 50
 }
 return $child
}
function Stop-Wo26($Child,[string]$Verb){
 try{$Child.Kill($true);$Child.WaitForExit();$records.Add(@{Source=$source;Command='WO25Probe '+$Verb+' <SYNTHETIC_ROOT>';Input=$Verb;Termination='Parent killed separate process; not reboot';Output=$Child.StandardOutput.ReadToEnd();ExitCode=$Child.ExitCode;Environment='Windows separate process'})}finally{$Child.Dispose()}
}
function Fresh {
 $root=Join-Path $env:RUNNER_TEMP ('wo25-wo26-'+[guid]::NewGuid().ToString('N'));$null=Run init $root;return $root
}
try{
 foreach($mode in @('read','write','exclusive')){
  $root=Fresh;$child=Start-Wo26 ('hold:'+$mode) $root
  try{$r=Run apply $root;if($r.Diagnostic.NativeErrorCode -ne 32 -or $r.AnyMutationStarted -or $r.RecoveryDisposition -ne 'PreMutationRejected'){throw 'WO26_PREMUTATION_CLASSIFICATION'}}finally{Stop-Wo26 $child ('hold:'+$mode)}
  $null=Run check $root
 }
 $root=Fresh;$r=Run readonly $root
 if($r.RecoveryDisposition -ne 'PreMutationRejected' -or !$r.OriginalBytesVerified){throw 'WO26_READONLY_CLASSIFICATION'}
 $null=Run check $root
 foreach($change in @('release','identity','backup','package','content')){
  $root=Fresh;$null=Run defer $root
  $child=Start-Wo26 'hold:exclusive' $root
  try{$r=Run reconcile $root;if($r.CanRequestRecovery){throw 'WO26_LOCKED_RESUME_ENABLED'}}finally{Stop-Wo26 $child 'hold:exclusive'}
  $target=Join-Path $root 'install/app.txt';$transactions=@(Get-ChildItem -LiteralPath (Join-Path $root 'work') -Directory -Filter 'tx-*');if($transactions.Count -ne 1){throw 'WO26_SINGLE_TRANSACTION_REQUIRED'};$tx=$transactions[0]
  if($change -eq 'identity'){Move-Item -LiteralPath $target -Destination ($target+'.old');[IO.File]::WriteAllText($target,'original bytes')}
  if($change -eq 'backup'){[IO.File]::WriteAllText((Join-Path $tx.FullName 'backup/app.txt'),'modified backup')}
  if($change -eq 'package'){[IO.File]::WriteAllText((Join-Path $tx.FullName 'package.spdup'),'invalid package')}
  if($change -eq 'content'){[IO.File]::WriteAllText($target,'external content')}
  $before=(Get-FileHash -LiteralPath $target).Hash
  $r=Run reconcile $root
  if($r.CanRequestRecovery -ne ($change -eq 'release')){throw 'WO26_RECONCILIATION_SCOPE'}
  $null=Run resume $root $(if($change -eq 'release'){@(0)}else{@(10)})
  if((Get-FileHash -LiteralPath $target).Hash -ne $before){throw 'WO26_UNEXPECTED_CONTENT_MUTATION'}
  if($change -eq 'release'){$null=Run check $root}
 }
 foreach($point in @('AfterRollbackStarted','DuringRollbackCopy','BeforeRestoredJournal')){
  $root=Fresh;$child=Start-Wo26 'kill:AfterFirstReplacement' $root;Stop-Wo26 $child 'kill:AfterFirstReplacement'
  $r=Run reconcile $root;if(!$r.CanRequestRecovery){throw 'WO26_AUTHENTICATED_RESUME_UNAVAILABLE'}
  $child=Start-Wo26 ('kill-recovery:'+$point) $root;Stop-Wo26 $child ('kill-recovery:'+$point)
  $null=Run reconcile $root;$null=Run resume $root;$null=Run check $root
 }
 . (Join-Path $PSScriptRoot 'wo25-input-closure.ps1')
 $priorRuntime=[Environment]::GetEnvironmentVariable('DOTNET_ADDITIONAL_DEPS','Process')
 try {
  [Environment]::SetEnvironmentVariable('DOTNET_ADDITIONAL_DEPS','synthetic-untrusted-deps','Process')
  $rejected=$false
  try{Assert-Wo26RuntimeEnvironment}catch{$rejected=$_.Exception.Message -eq 'DEPLOYMENT_RUNTIME_ENVIRONMENT_REVIEW'}
  if(!$rejected){throw 'WO26_RUNTIME_OVERRIDE_ACCEPTED'}
  $records.Add(@{Source=$source;Command='Assert-Wo26RuntimeEnvironment';Input='Synthetic process-local additional-deps override';Output='DEPLOYMENT_RUNTIME_ENVIRONMENT_REVIEW';ExitCode=0;Environment='PowerShell process only; prior environment restored'})
 }finally{[Environment]::SetEnvironmentVariable('DOTNET_ADDITIONAL_DEPS',$priorRuntime,'Process')}
 'WO26_PROCESS_RECOVERY=PASS; GUI=NOT_TESTED; POWER_LOSS=NOT_TESTED'
}finally{$records|ConvertTo-Json -Depth 18|Set-Content (Join-Path $out 'process-results.json')}
