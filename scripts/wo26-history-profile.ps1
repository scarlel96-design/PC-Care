param([string]$OutputDirectory='artifacts/wo26-history',[int[]]$Counts=@(0,1,10,100,500,1000,4096))
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$out=Join-Path $repo $OutputDirectory;New-Item -ItemType Directory -Force $out|Out-Null
$probe=Join-Path $repo 'tools/SmartPerformanceDoctor.WO25Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO25Probe.exe'
if(!$env:RUNNER_TEMP){throw 'WO26_RUNNER_TEMP_REQUIRED'}
$source=(& git -C $repo rev-parse HEAD).Trim();$records=[Collections.Generic.List[object]]::new()
try {
 foreach($count in $Counts){
  if($count -lt 0 -or $count -gt 4096){throw 'WO26_COUNT_LIMIT'}
  $root=Join-Path $env:RUNNER_TEMP ('wo25-history-'+[guid]::NewGuid().ToString('N'))
  $raw=& $probe ('history-init:'+$count) $root;$code=$LASTEXITCODE
  if($code -ne 0){throw 'WO26_PROFILE_SETUP_FAILED'}
  $repeats=if($count -in 1,100){3}else{1}
  for($iteration=0;$iteration -lt $repeats;$iteration++){
   $start=[Diagnostics.ProcessStartInfo]::new($probe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
   $start.ArgumentList.Add('history-profile');$start.ArgumentList.Add($root)
   $child=[Diagnostics.Process]::Start($start);$timer=[Diagnostics.Stopwatch]::StartNew();$budget=$false
   try{
    while(!$child.WaitForExit(100)){
     $child.Refresh()
     if($timer.Elapsed.TotalSeconds -gt 120 -or $child.WorkingSet64 -gt 536870912){$budget=$true;$child.Kill($true);$child.WaitForExit();break}
    }
    $record=@{Source=$source;Environment='Windows synthetic signed history; strictAcl=false; constructed rather than applied transactions';Command='WO25Probe history-profile <SYNTHETIC_ROOT>';Input=@{Transactions=$count;Iteration=$iteration;TimeoutSeconds=120;MemoryCeilingBytes=536870912};Output=$child.StandardOutput.ReadToEnd();ExitCode=$child.ExitCode;Status=$(if($budget){'BUDGET_STOPPED'}else{'PASS'})}
    $records.Add($record)
    if(!$budget -and $child.ExitCode -ne 0){throw ('WO26_HISTORY_REJECTED:'+ $child.StandardError.ReadToEnd())}
   }finally{$child.Dispose()}
  }
 }
 'WO26_HISTORY_PROFILE_RECORDED'
}finally{$records|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'history-profile.json')}
