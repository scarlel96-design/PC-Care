param([string]$OutputDirectory='artifacts/wo25-ci')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$out=Join-Path $repo $OutputDirectory;New-Item -ItemType Directory -Force $out|Out-Null
$source=(& git -C $repo rev-parse HEAD).Trim()
$probe=Join-Path $repo 'tools/SmartPerformanceDoctor.WO25Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO25Probe.exe'
if(!$env:RUNNER_TEMP -or ![IO.Path]::IsPathFullyQualified($env:RUNNER_TEMP)){throw 'WO25_RUNNER_TEMP_REQUIRED'}
$records=[Collections.Generic.List[object]]::new()
function Invoke-Wo25([string]$Verb,[string]$Root){
 $raw=& $probe $Verb $Root;$code=$LASTEXITCODE
 $records.Add(@{Source=$source;Environment='Windows synthetic filesystem; strictAcl=false; ephemeral fixture key';Command='WO25Probe '+$Verb+' <SYNTHETIC_ROOT>';Input=$Verb;Output=($raw -join "`n");ExitCode=$code})
 if($code -ne 0){throw "WO25_PROBE_FAILED:$Verb"}
 return ($raw|ConvertFrom-Json)
}
function Start-Wo25([string]$Verb,[string]$Root){
 $signal=Join-Path $Root 'ready.signal';if(Test-Path -LiteralPath $signal){Remove-Item -LiteralPath $signal}
 $start=[Diagnostics.ProcessStartInfo]::new($probe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $start.ArgumentList.Add($Verb);$start.ArgumentList.Add($Root)
 $child=[Diagnostics.Process]::Start($start)
 $timer=[Diagnostics.Stopwatch]::StartNew()
 while(!(Test-Path -LiteralPath $signal)){
  if($child.HasExited -or $timer.Elapsed.TotalSeconds -gt 30){if(!$child.HasExited){$child.Kill($true);$child.WaitForExit()};$text=$child.StandardError.ReadToEnd();$child.Dispose();throw ('WO25_SIGNAL_MISSING:'+ $Verb+':'+$text)}
  Start-Sleep -Milliseconds 100
 }
 return $child
}
function Stop-Wo25($Child,[string]$Verb){
 try{$Child.Kill($true);$Child.WaitForExit();$records.Add(@{Source=$source;Environment='Windows synthetic separate process; not reboot or physical power loss';Command='WO25Probe '+$Verb+' <SYNTHETIC_ROOT>';Input=$Verb;Termination='Parent killed separate process';Output=$Child.StandardOutput.ReadToEnd();ExitCode=$Child.ExitCode})}finally{$Child.Dispose()}
}
try {
 foreach($mode in @('read','write','exclusive')){
  $root=Join-Path $env:RUNNER_TEMP ('wo25-lock-'+[guid]::NewGuid().ToString('N'));$null=Invoke-Wo25 init $root
  $child=Start-Wo25 ('hold:'+$mode) $root
  try{$result=Invoke-Wo25 apply $root;if($result.Diagnostic.NativeErrorCode -ne 32 -or $result.AnyMutationStarted){throw 'WO25_SHARING_DIAGNOSTIC'}}finally{Stop-Wo25 $child ('hold:'+$mode)}
  $null=Invoke-Wo25 check $root
 }
 $root=Join-Path $env:RUNNER_TEMP ('wo25-readonly-'+[guid]::NewGuid().ToString('N'));$null=Invoke-Wo25 init $root
 $result=Invoke-Wo25 readonly $root;if($result.Diagnostic.NativeErrorCode -ne 5 -or $result.AnyMutationStarted){throw 'WO25_READONLY_DIAGNOSTIC'};$null=Invoke-Wo25 check $root
 foreach($point in @('BeforeNewFileRename','AfterNewFileRename')){
  $root=Join-Path $env:RUNNER_TEMP ('wo25-publish-'+[guid]::NewGuid().ToString('N'));$null=Invoke-Wo25 init $root
  $child=Start-Wo25 ('kill:'+$point) $root;Stop-Wo25 $child ('kill:'+$point)
  $null=Invoke-Wo25 recover $root;$null=Invoke-Wo25 check $root
 }
 if($env:GITHUB_ACTIONS -eq 'true' -and $env:RUNNER_ENVIRONMENT -eq 'github-hosted'){
  foreach($point in @('normal','BeforeVolumePreflight','CrossVolumeRejected')){
   $root=Join-Path $env:RUNNER_TEMP ('wo25-cross-'+[guid]::NewGuid().ToString('N'));$prepared=Invoke-Wo25 cross-init $root
   if(!$prepared.ActualDifferentVolumes){$records.Add(@{Status='NOT TESTED';Source=$source;Input=$point;Output='Distinct actual volumes unavailable';ExitCode=$null});continue}
   if($point -eq 'normal'){$result=Invoke-Wo25 apply $root;if($result.Code -ne 'PUBLISH-SAME-VOLUME-REQUIRED' -or $result.AnyMutationStarted){throw 'WO25_CROSS_VOLUME_REFUSAL'}}
   else{$child=Start-Wo25 ('kill:'+$point) $root;Stop-Wo25 $child ('kill:'+$point)}
   $check=Invoke-Wo25 check $root;if($check.Transactions -ne 0){throw 'WO25_UNEXPECTED_TRANSACTION'}
  }
 } else {$records.Add(@{Status='NOT TESTED';Source=$source;Command='WO25Probe cross-init';Input='Actual second volume';Output='Hosted disposable cross-volume environment is required';ExitCode=$null})}
 $root=Join-Path $env:RUNNER_TEMP ('wo25-closure-'+[guid]::NewGuid().ToString('N'));$prepared=Invoke-Wo25 closure-init $root
 . (Join-Path $PSScriptRoot 'wo25-input-closure.ps1')
 $binding=Open-Wo25InputClosure (Join-Path $root 'inputs') (Join-Path $root 'manifest.json') (Join-Path $root 'signature.bin') (Join-Path $root 'public.bin') $prepared.KeyHash
 foreach($stream in $binding.Leases){$stream.Dispose()}
 $path=Join-Path $root 'inputs/runtime.json';[IO.File]::WriteAllText($path,'{"changed":true}')
 $rejected=$false;try{$bad=Open-Wo25InputClosure (Join-Path $root 'inputs') (Join-Path $root 'manifest.json') (Join-Path $root 'signature.bin') (Join-Path $root 'public.bin') $prepared.KeyHash;foreach($stream in $bad.Leases){$stream.Dispose()}}catch{$rejected=$_.Exception.Message -eq 'DEPLOYMENT_CONTENT_REJECTED'}
 if(!$rejected){throw 'WO25_DEPLOYMENT_TAMPER_ACCEPTED'}
 $records.Add(@{Source=$source;Environment='Actual Windows synthetic files; .NET ECDSA, SHA256 and read leases; no PE execution';Command='Open-Wo25InputClosure';Input='Correct signed closure, then modified runtime.json';Output=@{AuthenticatedContentBound=$true;RuntimeTamperRejected=$rejected;ActualPeDeployment=$false};ExitCode=0})
 'WO25_PROCESS_AND_CLOSURE=PASS; CROSS_VOLUME_SUPPORT=BLOCKED; ACTUAL_GUI=NOT_TESTED'
} finally {$records|ConvertTo-Json -Depth 16|Set-Content (Join-Path $out 'process-results.json')}
