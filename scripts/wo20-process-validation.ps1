param([string]$OutputDirectory="artifacts/wo20-process")
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$output=[IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
if(-not $output.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'OutputOutsideWorkspace'}
New-Item -ItemType Directory -Force $output | Out-Null
if([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)){throw 'WO20_TEST_ROOT_REQUIRED'}
$parent=[IO.Path]::GetFullPath($env:RUNNER_TEMP)
if($parent.StartsWith((Join-Path $repo 'tests'),[StringComparison]::OrdinalIgnoreCase)){throw 'WO20_TEST_ROOT_PROTECTED'}
New-Item -ItemType Directory -Force $parent | Out-Null
$probe=Join-Path $repo 'tools/SmartPerformanceDoctor.WO20Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO20Probe.exe'
$records=[Collections.Generic.List[object]]::new()
function Invoke-Probe([string]$Verb,[string]$Root){
 $text=& $probe $Verb $Root 2>&1
 $code=$LASTEXITCODE
 $records.Add(@{Command="WO20Probe $Verb <SYNTHETIC_ROOT>";Input=$Verb;Output=($text|Out-String).Trim();ExitCode=$code;Environment='Windows synthetic filesystem; strictAcl=false; ephemeral package key; no Authenticode E2E'})
 if($code -ne 0){throw "Probe failed $Verb : $text"}
}
function Kill-Probe([string]$Verb,[string]$Root){
 $out=Join-Path $Root 'worker-output.txt';$err=Join-Path $Root 'worker-error.txt'
 $process=Start-Process -FilePath $probe -ArgumentList @($Verb,('"'+$Root+'"')) -WindowStyle Hidden -RedirectStandardOutput $out -RedirectStandardError $err -PassThru
 try{
  $signal=Join-Path $Root 'checkpoint.signal'
  $timer=[Diagnostics.Stopwatch]::StartNew()
  while(!(Test-Path -LiteralPath $signal)-and !$process.HasExited -and $timer.Elapsed.TotalSeconds -lt 60){Start-Sleep -Milliseconds 100}
  if(!(Test-Path -LiteralPath $signal)){throw "Checkpoint not reached: $Verb $(Get-Content $err -Raw)"}
  $checkpoint=Get-Content -LiteralPath $signal -Raw
  $process.Kill();$process.WaitForExit();$code=$process.ExitCode
  $records.Add(@{Command="WO20Probe $Verb <SYNTHETIC_ROOT>";Checkpoint=$checkpoint;ExitCode=$code;Output=(Get-Content $out -Raw).Trim();Termination='Parent killed separate process';Environment='Synthetic filesystem'})
  if($code -ne -1){throw "Unexpected termination exit $code"}
 }finally{if(!$process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}
}
try{
 foreach($kind in @('basic','random','many')){
  $root=Join-Path $parent ('wo20-'+$kind+'-'+[guid]::NewGuid().ToString('N'))
  Invoke-Probe $(if($kind -eq 'basic'){'prepare'}else{'prepare-'+$kind}) $root
  Invoke-Probe 'vault-check' $root;Invoke-Probe 'apply' $root;Invoke-Probe 'vault-check' $root
 }
 $root=Join-Path $parent ('wo20-update-crash-'+[guid]::NewGuid().ToString('N'))
 Invoke-Probe 'prepare' $root;Invoke-Probe 'vault-check' $root;Kill-Probe 'apply-kill' $root
 Invoke-Probe 'vault-check' $root;Invoke-Probe 'recover' $root;Invoke-Probe 'verify-original' $root;Invoke-Probe 'vault-check' $root
 foreach($point in @('AuthorizationVerified','BeforePromotion','AfterPromotion','BeforeRestore','AfterFileRestore','SecondFileRestore','BeforeRestoredJournal','AfterRestoredJournal')){
  $root=Join-Path $parent ('wo20-legacy-'+[guid]::NewGuid().ToString('N'))
  Invoke-Probe 'prepare' $root;Invoke-Probe 'vault-check' $root;Invoke-Probe 'legacy-prepare' $root
  Kill-Probe ('legacy-kill:'+$point) $root
  Invoke-Probe 'vault-check' $root
  Invoke-Probe $(if($point -in @('AuthorizationVerified','BeforePromotion')){'legacy-restore'}else{'recover'}) $root
  Invoke-Probe 'verify-original' $root;Invoke-Probe 'vault-check' $root
 }
 'WO20_WINDOWS_SYNTHETIC=PASS; AUTHENTICODE_E2E=NOT_TESTED; REBOOT=NOT_TESTED'
}finally{$records|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $output 'process-results.json') -Encoding UTF8}
