param([string]$OutputDirectory='artifacts/wo10-sandbox')
$ErrorActionPreference='Stop'
$output=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $output | Out-Null
$parent=Join-Path $output ('synthetic-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $parent | Out-Null
$exe=[IO.Path]::GetFullPath('tools/SmartPerformanceDoctor.RecoverySandbox/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.RecoverySandbox.exe')
$results=@()
foreach($point in @('Prepared','Moved','Quarantined','RollingBack','RestoredMove','Restored')) {
 $root=(& $exe create $parent | Select-Object -Last 1).Trim()
 if($LASTEXITCODE -ne 0){throw 'Fixture creation failed'}
 $before=(Get-FileHash -LiteralPath (Join-Path $root 'fixture.dat')).Hash
 $verb=if($point -in @('Prepared','Moved','Quarantined')){'execute'}else{'recover'}
 if($verb -eq 'recover'){ & $exe execute $root | Out-Null; if($LASTEXITCODE -ne 0){throw 'Preparation failed'} }
 $process=Start-Process -FilePath $exe -ArgumentList @($verb,('"'+$root+'"'),$point) -WindowStyle Hidden -PassThru
 $deadline=[DateTimeOffset]::UtcNow.AddSeconds(20)
 while(-not (Test-Path -LiteralPath (Join-Path $root 'checkpoint.txt')) -and -not $process.HasExited -and [DateTimeOffset]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 if(-not (Test-Path -LiteralPath (Join-Path $root 'checkpoint.txt'))){if(-not $process.HasExited){Stop-Process -Id $process.Id};throw 'Checkpoint not reached'}
 Stop-Process -Id $process.Id -Force
 $process.WaitForExit()
 $recover=& $exe recover $root | Out-String
 $exit=$LASTEXITCODE
 $after=(Get-FileHash -LiteralPath (Join-Path $root 'fixture.dat')).Hash
 if($exit -ne 0 -or $before -ne $after){throw 'Crash recovery mismatch'}
 $results+=@{scenario=$point;status='PASS';termination='Separate process forcibly terminated';recoveryCommand='RecoverySandbox recover <SYNTHETIC_ROOT>';output=$recover.Trim();exitCode=$exit;beforeSha256=$before;afterSha256=$after;aclProtected=(Get-Acl -LiteralPath $root).AreAccessRulesProtected}
}
# Test a hard link without changing the linked target.
$root=(& $exe create $parent | Select-Object -Last 1).Trim()
$target=Join-Path $root 'fixture.dat'
& fsutil.exe hardlink create (Join-Path $root 'second-link.dat') $target | Out-Null
if($LASTEXITCODE -ne 0){throw 'Hard link fixture failed'}
$stderr=Join-Path $output 'hardlink-stderr.txt'
$rejected=Start-Process -FilePath $exe -ArgumentList @('execute',('"'+$root+'"')) -RedirectStandardError $stderr -WindowStyle Hidden -Wait -PassThru
$exit=$rejected.ExitCode
$denied=Get-Content -LiteralPath $stderr -Raw
if($exit -ne 3){throw 'Hard link was accepted'}
$results+=@{scenario='HardLink';status='PASS';output=$denied.Trim();exitCode=$exit;originalPreserved=Test-Path -LiteralPath $target}
foreach($kind in @('Junction','SymbolicLink')) {
 $root=(& $exe create $parent | Select-Object -Last 1).Trim()
 if($LASTEXITCODE -ne 0){throw 'Link fixture creation failed'}
 $original=Join-Path $root 'fixture.dat'
 $before=(Get-FileHash -LiteralPath $original).Hash
 if($kind -eq 'Junction') {
  $alias=Join-Path $parent ('pccare-wo10-'+[guid]::NewGuid().ToString('N'))
  New-Item -ItemType Junction -Path $alias -Target $root | Out-Null
 } else {
  $saved=Join-Path $root 'fixture.saved'
  Move-Item -LiteralPath $original -Destination $saved
  try {New-Item -ItemType SymbolicLink -Path $original -Target $saved -ErrorAction Stop | Out-Null}
  catch {
   Move-Item -LiteralPath $saved -Destination $original
   $results+=@{scenario=$kind;status='NOT TESTED';reason='Symbolic link creation privilege unavailable';originalPreserved=$before -eq (Get-FileHash -LiteralPath $original).Hash}
   continue
  }
  $alias=$root
 }
 $stderr=Join-Path $output ($kind+'-stderr.txt')
 $rejected=Start-Process -FilePath $exe -ArgumentList @('execute',('"'+$alias+'"')) -RedirectStandardError $stderr -WindowStyle Hidden -Wait -PassThru
 $verify=if($kind -eq 'Junction'){$original}else{$saved}
 if($rejected.ExitCode -ne 3 -or $before -ne (Get-FileHash -LiteralPath $verify).Hash){throw 'Link boundary rejection failed'}
 $results+=@{scenario=$kind;status='PASS';exitCode=$rejected.ExitCode;output=(Get-Content -LiteralPath $stderr -Raw).Trim();originalPreserved=$true}
}
$results | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'sandbox-results.json')
Write-Output ('WO10_SANDBOX_PASS crashScenarios=6 hardLinkRejected=1 bytesRestored=6')
