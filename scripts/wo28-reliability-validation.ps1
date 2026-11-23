param([string]$OutputDirectory='artifacts/wo28-reliability')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(!$env:RUNNER_TEMP -or ![IO.Path]::IsPathFullyQualified($env:RUNNER_TEMP)){throw 'ABSOLUTE_RUNNER_TEMP_REQUIRED'}
$temp=[IO.Path]::GetFullPath($env:RUNNER_TEMP).TrimEnd('\')
$repoFull=[IO.Path]::GetFullPath($repo).TrimEnd('\')
if($temp -eq $repoFull -or $temp.StartsWith($repoFull+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'RUNNER_TEMP_MUST_BE_OUTSIDE_REPOSITORY'}
foreach($protected in @($env:WINDIR,$env:ProgramFiles,${env:ProgramFiles(x86)})){
 if($protected -and ($temp -eq $protected -or $temp.StartsWith($protected.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase))){throw 'PROTECTED_RUNNER_TEMP_REJECTED'}
}
$node=Get-Item -LiteralPath $temp
while($null -ne $node){
 if(($node.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'RUNNER_TEMP_REPARSE_REJECTED'}
 $node=$node.Parent
}
$out=Join-Path $repo $OutputDirectory
New-Item -ItemType Directory -Force $out|Out-Null
$wo27=Join-Path $repo 'tools/SmartPerformanceDoctor.WO27Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO27Probe.exe'
$probe=Join-Path $repo 'tools/SmartPerformanceDoctor.WO28ReliabilityProbe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO28ReliabilityProbe.exe'
foreach($binary in @($wo27,$probe)){if(!(Test-Path -LiteralPath $binary)){throw 'BUILD_CURRENT_PROBES_REQUIRED'}}
$source=(& git -C $repo rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0){throw 'SOURCE_LOOKUP_FAILED'}
$sourceChanges=@(& git -C $repo status --porcelain -- src tools scripts tests .github)
$sourceClassification=if($sourceChanges.Count){'BaseCommitPlusUncommittedSource'}else{'CleanCommittedSource'}
$commands=[Collections.Generic.List[object]]::new()
$records=[Collections.Generic.List[object]]::new()
$boundaries=[Collections.Generic.List[object]]::new()
function Run-Probe([string]$Binary,[string[]]$Arguments,[int[]]$Expected=@(0)){
 $raw=& $Binary @Arguments
 $code=$LASTEXITCODE
 $record=@{Command=([IO.Path]::GetFileName($Binary)+' '+($Arguments|ForEach-Object{if([IO.Path]::IsPathFullyQualified($_)){'<SYNTHETIC_ROOT>'}else{$_}}|Join-String -Separator ' '));ExitCode=$code;Output=($raw -join "`n")}
 $commands.Add($record)
 if($code -notin $Expected){throw ('PROBE_FAILED:'+ $code)}
 $record
}
function Inspect-Root([string]$Root,[string]$Control){
 $result=Run-Probe $probe @('inspect',$Root,$Control)
 $result.Output|ConvertFrom-Json
}
function Stop-Inventory([string]$Root,[string]$Control){
 $signal=Join-Path $Control 'ready.json'
 if(Test-Path -LiteralPath $signal){Remove-Item -LiteralPath $signal}
 $start=[Diagnostics.ProcessStartInfo]::new($probe)
 $start.UseShellExecute=$false;$start.CreateNoWindow=$true
 $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 foreach($arg in @('hold-inventory',$Root,$Control)){$start.ArgumentList.Add($arg)}
 $child=[Diagnostics.Process]::Start($start)
 $watch=[Diagnostics.Stopwatch]::StartNew()
 try{
  while(!(Test-Path -LiteralPath $signal) -and !$child.HasExited -and $watch.Elapsed.TotalSeconds -lt 90){Start-Sleep -Milliseconds 20}
  if(!(Test-Path -LiteralPath $signal)){
   if(!$child.HasExited){$child.Kill($true);$child.WaitForExit()}
   throw ('READONLY_CHECKPOINT_NOT_REACHED:'+ $child.StandardError.ReadToEnd())
  }
  $writeBlocked=$false
  try{
   $attempt=[IO.FileStream]::new((Join-Path $Root 'install/app.txt'),[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite)
   $attempt.Dispose()
  }catch [IO.IOException]{$writeBlocked=$true}
  if(!$writeBlocked){throw 'INVENTORY_WRITER_EXCLUSION_FAILED'}
  $child.Kill($true);$child.WaitForExit()
  $checkpoint=Get-Content -LiteralPath $signal -Raw|ConvertFrom-Json
  $record=@{Command='WO28ReliabilityProbe hold-inventory <FIXTURE_ROOT> <CONTROL_ROOT>; parent Process.Kill(true)';ExitCode=$child.ExitCode;KilledByParent=$true;Output=$child.StandardOutput.ReadToEnd();Checkpoint=$checkpoint;ConcurrentWriterOpenBlocked=$writeBlocked}
  $commands.Add($record)
  $record
 }finally{if(!$child.HasExited){$child.Kill($true);$child.WaitForExit()};$child.Dispose()}
}
try{
 $boundaryControl=Join-Path $temp ('wo28-control-'+[guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory $boundaryControl|Out-Null
 [IO.File]::WriteAllText((Join-Path $boundaryControl 'control.marker'),'WO28_CONTROL_ONLY')
 $relativeRejected=Run-Probe $probe @('inspect','relative-fixture',$boundaryControl) @(3)
 $boundaries.Add(@{Name='RelativeRootRejected';Status='PASS';Execution=$relativeRejected})
 foreach($kind in @('MissingMarker','Junction','Hardlink')){
  $boundaryRoot=Join-Path $temp ('wo27-wo28-boundary-'+[guid]::NewGuid().ToString('N'))
  $null=Run-Probe $wo27 @('init:4096:repeat',$boundaryRoot)
  if($kind -ne 'MissingMarker'){[IO.File]::WriteAllText((Join-Path $boundaryRoot 'wo28.marker'),'WO28_READONLY_RELIABILITY')}
  if($kind -eq 'Junction'){New-Item -ItemType Junction -Path (Join-Path $boundaryRoot 'unsafe-junction') -Target $boundaryControl|Out-Null}
  if($kind -eq 'Hardlink'){New-Item -ItemType HardLink -Path (Join-Path $boundaryRoot 'duplicate.bin') -Target (Join-Path $boundaryRoot 'install/app.txt')|Out-Null}
  $rejected=Run-Probe $probe @('inspect',$boundaryRoot,$boundaryControl) @(3)
  $boundaries.Add(@{Name=($kind+'Rejected');Status='PASS';Execution=$rejected})
 }
 foreach($case in @(@{Bytes=4096;State='OriginalVerified'},@{Bytes=1048576;State='UpdatedVerified'},@{Bytes=16777216;State='UnknownContent'},@{Bytes=67108864;State='UnknownContent'})){
  $root=Join-Path $temp ('wo27-wo28-readonly-'+[guid]::NewGuid().ToString('N'))
  $control=Join-Path $temp ('wo28-control-'+[guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory $control|Out-Null
  [IO.File]::WriteAllText((Join-Path $control 'control.marker'),'WO28_CONTROL_ONLY')
  $prepared=Run-Probe $wo27 @(('init:'+$case.Bytes+':repeat'),$root)
  $preparedInput=$prepared.Output|ConvertFrom-Json
  $null=Run-Probe $wo27 @('defer',$root)
  [IO.File]::WriteAllText((Join-Path $root 'wo28.marker'),'WO28_READONLY_RELIABILITY')
  if($case.State -ne 'OriginalVerified'){
   $block=[byte[]]::new(65536);[Array]::Fill($block,[byte]0x72)
   $stream=[IO.FileStream]::new((Join-Path $root 'install/app.txt'),[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::None)
   try{
    $remaining=[long]$case.Bytes
    while($remaining -gt 0){$count=[int][Math]::Min($remaining,$block.Length);$stream.Write($block,0,$count);$remaining-=$count}
    if($case.State -eq 'UnknownContent'){$stream.Position=0;$stream.WriteByte(0x41)}
    $stream.Flush($true)
   }finally{$stream.Dispose();[Array]::Clear($block)}
  }
  $before=Inspect-Root $root $control
  if($before.Reconciliation.Files[0].State -ne $case.State){throw 'EXPECTED_RECONCILIATION_STATE_MISMATCH'}
  $kills=[Collections.Generic.List[object]]::new()
  foreach($repeat in 1..2){
   $kills.Add((Stop-Inventory $root $control))
   $again=Inspect-Root $root $control
   if($again.InventoryDigest -ne $before.InventoryDigest -or ($again.Reconciliation|ConvertTo-Json -Depth 12 -Compress) -ne ($before.Reconciliation|ConvertTo-Json -Depth 12 -Compress)){throw 'READONLY_RESTART_MUTATED_INPUT'}
  }
  $resume=Run-Probe $wo27 @('resume',$root) $(if($case.State -eq 'UnknownContent'){@(10)}else{@(0)})
  $after=Inspect-Root $root $control
  if($case.State -eq 'UnknownContent' -and $after.InventoryDigest -ne $before.InventoryDigest){throw 'UNKNOWN_RESUME_MUTATED_INPUT'}
  $backups=@($after.Inventory|Where-Object{$_.Kind -eq 'File' -and $_.RelativePath -match '/backup/app.txt$'})
  if($backups.Count -ne 1 -or $backups[0].Sha256 -ne $preparedInput.OriginalHash){throw 'AUTHENTICATED_BACKUP_NOT_PRESERVED'}
  if($case.State -ne 'UnknownContent'){
   $target=$after.Inventory|Where-Object{$_.RelativePath -eq 'install/app.txt'}
   if($target.Sha256 -ne $preparedInput.OriginalHash){throw 'KNOWN_CONTENT_RECOVERY_FAILED'}
  }
  $records.Add(@{Status='PASS';Input=$case;Before=$before;Terminations=$kills;Resume=$resume;After=$after;FullResidualInventoryVerified=$true;ExcludedFileCount=0;QueryRestartPreservedAllInputs=$true;EngineInternalReconciliationTermination='NOT TESTED';PhysicalPowerLoss='NOT TESTED'})
 }
 'WO28_READONLY_INVENTORY_RESTART_PASS='+$records.Count
}finally{
 @{Source=$source;SourceClassification=$sourceClassification;Environment='Windows synthetic filesystem; RUNNER_TEMP outside repository; strictAcl=false';Scope='ReconcileRecovery returns, then parent kills the read-only full-inventory hash phase. The engine call is not interrupted.';Records=$records;BoundaryTests=$boundaries;Commands=$commands;ProbeSha256=(Get-FileHash -LiteralPath $probe -Algorithm SHA256).Hash;SetupProbeSha256=(Get-FileHash -LiteralPath $wo27 -Algorithm SHA256).Hash;ProgramSha256=(Get-FileHash -LiteralPath (Join-Path $repo 'tools/SmartPerformanceDoctor.WO28ReliabilityProbe/Program.cs') -Algorithm SHA256).Hash;ScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash;EngineSha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $probe) 'SmartPerformanceDoctor.UpdateEngine.dll') -Algorithm SHA256).Hash;ProductionEngineChanged=$false}|ConvertTo-Json -Depth 24|Set-Content -LiteralPath (Join-Path $out 'reliability.json')
}
