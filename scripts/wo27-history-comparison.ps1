$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent;$out=Join-Path $repo 'artifacts/wo27-history-comparison';New-Item -ItemType Directory -Force $out|Out-Null
if(!$env:RUNNER_TEMP){throw 'RUNNER_TEMP_REQUIRED'}
$shadow=Join-Path $env:RUNNER_TEMP ('wo27-history-comparison-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Force $shadow|Out-Null
$paths=@{}
foreach($kind in @('baseline','modified')){
 $dir=Join-Path $shadow $kind;New-Item -ItemType Directory -Force (Join-Path $dir 'Engine')|Out-Null
 foreach($file in Get-ChildItem (Join-Path $repo 'src/SmartPerformanceDoctor.UpdateEngine/*.cs')){
  $target=Join-Path $dir ('Engine/'+$file.Name)
  if($kind -eq 'baseline'){ $raw=& git -C $repo show ('20364a068de9c3d8e919a1de1c10e990981c7de8:src/SmartPerformanceDoctor.UpdateEngine/'+$file.Name);if($LASTEXITCODE -eq 0){[IO.File]::WriteAllText($target,($raw -join "`n"))} }
  else {Copy-Item -LiteralPath $file.FullName -Destination $target}
 }
 Copy-Item (Join-Path $repo 'tools/SmartPerformanceDoctor.WO25Probe/Program.cs') (Join-Path $dir 'Program.cs')
 [IO.File]::WriteAllText((Join-Path $dir 'Profile.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0-windows10.0.26100.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
 & dotnet build (Join-Path $dir 'Profile.csproj') -c Release -v:minimal *> (Join-Path $out ($kind+'-build.txt'))
 if($LASTEXITCODE -ne 0){throw ('COMPARISON_BUILD_FAILED:'+ $kind)}
 $paths[$kind]=Join-Path $dir 'bin/Release/net10.0-windows10.0.26100.0/Profile.dll'
}
$rows=[Collections.Generic.List[object]]::new()
function Profile([string]$Dll,[string]$Root){
 $start=[Diagnostics.ProcessStartInfo]::new('dotnet');$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 foreach($arg in @($Dll,'history-profile',$Root)){$start.ArgumentList.Add($arg)}
 $child=[Diagnostics.Process]::Start($start);$timer=[Diagnostics.Stopwatch]::StartNew()
 try{
  while(!$child.WaitForExit(100)){
   $child.Refresh()
   if($timer.Elapsed.TotalSeconds -gt 180 -or $child.WorkingSet64 -gt 536870912){$child.Kill($true);$child.WaitForExit();throw 'HISTORY_RESOURCE_BUDGET_EXCEEDED'}
  }
  if($child.ExitCode -ne 0){throw ('HISTORY_CHILD_FAILED:'+ $child.StandardError.ReadToEnd())}
  $child.StandardOutput.ReadToEnd()
 }finally{if(!$child.HasExited){$child.Kill($true);$child.WaitForExit()};$child.Dispose()}
}
try{
 foreach($count in @(0,1,10,100,500,1000,4096)){
  $root=Join-Path $env:RUNNER_TEMP ('wo25-history-'+[guid]::NewGuid().ToString('N'))
  & dotnet $paths.modified ('history-init:'+$count) $root | Out-Null
  if($LASTEXITCODE -ne 0){throw 'COMPARISON_INPUT_FAILED'}
  foreach($kind in @('baseline','modified','modified','baseline','baseline','modified')){
   $raw=Profile $paths[$kind] $root;$code=0
   $rows.Add(@{Kind=$kind;Source=(& git -C $repo rev-parse HEAD).Trim();BaselineSource='20364a068de9c3d8e919a1de1c10e990981c7de8';Command='dotnet '+$paths[$kind]+' history-profile <SAME_SYNTHETIC_ROOT>';Input=@{Transactions=$count;Order='ABBAAB';ConcurrentRegression=$false};Output=($raw -join "`n");ExitCode=$code;Environment='Windows; complete baseline/current engine copies; same synthetic signed history; strictAcl=false'})
   $rows|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'comparison.json')
   if($code -ne 0){throw 'COMPARISON_RUN_FAILED'}
  }
 }
 'WO27_HISTORY_COMPARISON_RECORDED'
}finally{$rows|ConvertTo-Json -Depth 8|Set-Content (Join-Path $out 'comparison.json')}
