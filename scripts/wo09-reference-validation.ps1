param([string]$OutputDirectory='artifacts/wo09-ci', [switch]$IsolatedRunner)
$ErrorActionPreference='Stop'
if (-not $IsolatedRunner -or $env:GITHUB_ACTIONS -ne 'true') { throw 'Isolated GitHub runner required for reference capture' }
$root=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $root | Out-Null
$private=Join-Path $env:RUNNER_TEMP ('wo09-private-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $private | Out-Null
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$acl=Get-Acl -LiteralPath $private
$acl.SetAccessRuleProtection($true,$false)
foreach($sid in @($identity.User.Value,'S-1-5-18','S-1-5-32-544')) {
 $rule=[Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', 'ContainerInherit,ObjectInherit','None','Allow')
 $acl.AddAccessRule($rule)
}
Set-Acl -LiteralPath $private -AclObject $acl
$probe=[IO.Path]::GetFullPath('tools/SmartPerformanceDoctor.DiagnosticsProbe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.DiagnosticsProbe.exe')
$analyzer=[IO.Path]::GetFullPath('tools/SmartPerformanceDoctor.ReferenceProbe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.ReferenceProbe.exe')
$profile=[IO.Path]::GetFullPath('scripts/wo09-reference.wprp')
$wpr=(Get-Command wpr.exe).Source
$status=& $wpr -status 2>&1 | Out-String
if ($status -notmatch 'not recording') { throw 'Existing WPR capture preserved; isolated session unavailable' }
$results=@()
foreach($trial in 1..6) {
 $dir=Join-Path $private "trial-$trial"
 New-Item -ItemType Directory $dir | Out-Null
 $measurement=Join-Path $dir 'measurement.json'
 $etl=Join-Path $dir 'reference.etl'
 $owned=$false
 $analysisComplete=$false
 $mode=if($trial -le 3){'cpu'}else{'peak'}
 try {
  $startOutput=& $wpr -start ($profile+'!WO09') -filemode 2>&1 | Out-String
  $startExit=$LASTEXITCODE
  if($startExit -ne 0) {throw "ReferenceStartFailed $startExit"}
  $owned=$true
  $load=Start-Process -FilePath $probe -ArgumentList @('--qualification-workload',$mode,'30',('"'+$dir+'"'),[Math]::Min(4,[Environment]::ProcessorCount)) -PassThru -WindowStyle Hidden
  & $probe 30 $measurement General
  $measureExit=$LASTEXITCODE
  if(-not $load.WaitForExit(40000)) {Stop-Process -Id $load.Id; throw 'Owned workload exceeded deadline'}
  if($load.ExitCode -ne 0 -or $measureExit -ne 0) {throw 'Controlled measurement failed'}
  $stopOutput=& $wpr -stop $etl 2>&1 | Out-String
  $stopExit=$LASTEXITCODE
  if($stopExit -ne 0) {throw "ReferenceStopFailed $stopExit"}
  $owned=$false
  $loadReport=Get-ChildItem -LiteralPath $dir -Filter 'workload-*.json' | Select-Object -First 1
  $workload=Get-Content -LiteralPath $loadReport.FullName -Raw | ConvertFrom-Json
  # Keep the complete trace until independent analysis has finished.
  $refOut=Join-Path $root "reference-$trial.json"
  & $analyzer $etl $measurement ([DateTimeOffset]::Parse($workload.startedUtc).AddSeconds(2).ToString('O')) ([DateTimeOffset]::Parse($workload.endedUtc).ToString('O')) $refOut
  $analysisExit=$LASTEXITCODE
  $analysisComplete=$analysisExit -eq 0
  Copy-Item -LiteralPath $measurement -Destination (Join-Path $root "measurement-$trial.json")
  Copy-Item -LiteralPath $loadReport.FullName -Destination (Join-Path $root "workload-$trial.json")
  $results+=@{trial=$trial;mode=$mode;status=if($analysisExit -eq 0){'PARTIAL'}else{'FAIL'};WorkloadExecuted=$true;
    startExit=$startExit;measureExit=$measureExit;stopExit=$stopExit;analysisExit=$analysisExit;
    etlSha256=(Get-FileHash -LiteralPath $etl -Algorithm SHA256).Hash;etlBytes=(Get-Item -LiteralPath $etl).Length;
    rawTracePublished=$false;retention='Until analysis completes; maximum CI job lifetime';rootAclProtected=(Get-Acl -LiteralPath $private).AreAccessRulesProtected;childAclInherited=-not (Get-Acl -LiteralPath $dir).AreAccessRulesProtected;
    stopReportedLoss=$stopOutput.Replace($private,'<PRIVATE_RUNNER_DIR>').Trim();order=$trial;PressureVerified='See independent qualification; workload alone is not truth'}
 } catch {
  $results+=@{trial=$trial;mode=$mode;status='FAIL';error=$_.Exception.Message.Replace($private,'<PRIVATE_RUNNER_DIR>');analysisExit=3;rawTracePublished=$false;retention='Private runner job lifetime; retry before disposal'}
 } finally {
  if($owned) { & $wpr -cancel | Out-Null }
  # Delete only traces inside the unique private runner directory.
  if($analysisComplete -and (Test-Path -LiteralPath $etl)) {
   $resolved=[IO.Path]::GetFullPath($etl)
   if(-not $resolved.StartsWith($private+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Private trace path escaped'}
   Remove-Item -LiteralPath $resolved
  }
 }
}
$results | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'reference-execution.json')
if($results.Where({$_.analysisExit -ne 0}).Count -gt 0) {throw 'Independent analyzer failed'}
Write-Output 'WO09_REFERENCE_ANALYSIS_COMPLETED trials=6 rawTracePublished=False'
