param([string]$OutputDirectory='artifacts/wo24-ci')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$out=Join-Path $repo $OutputDirectory;New-Item -ItemType Directory -Force $out|Out-Null
$source=(& git -C $repo rev-parse HEAD).Trim()
& (Join-Path $PSScriptRoot 'wo24-qualification.ps1') -Mode Prepare | Set-Content (Join-Path $out 'prepared.json')
$root=Join-Path $env:RUNNER_TEMP ('wo20-wo24-'+[guid]::NewGuid().ToString('N'))
$probe=Join-Path $repo 'tools/SmartPerformanceDoctor.WO20Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO20Probe.exe'
$records=[Collections.Generic.List[object]]::new()
foreach($verb in @('prepare','vault-check','apply','vault-check')){
 $raw=& $probe $verb $root;$code=$LASTEXITCODE
 $records.Add(@{Source=$source;Command=('WO20Probe '+$verb+' <SYNTHETIC_ROOT>');Input=$verb;Output=($raw|Out-String).Trim();ExitCode=$code})
 if($code -ne 0){throw "WO24_FIXTURE_FAILED:$verb"}
}
& (Join-Path $repo 'tools/SmartPerformanceDoctor.WO24Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO24Probe.exe') access $root | Set-Content (Join-Path $out 'current-token-access.json')
if($LASTEXITCODE -ne 0){throw 'WO24_ACCESS_PROBE_FAILED'}
$before=Join-Path $out 'before.json';$after=Join-Path $out 'after.json'
& (Join-Path $PSScriptRoot 'wo24-qualification.ps1') -Mode Capture -FixtureRoot $root -OutputFile $before
& (Join-Path $PSScriptRoot 'wo24-qualification.ps1') -Mode Capture -FixtureRoot $root -OutputFile $after
& (Join-Path $PSScriptRoot 'wo24-qualification.ps1') -Mode Compare -Before $before -After $after -OutputFile (Join-Path $out 'same-boot-comparison.json')
try{& (Join-Path $PSScriptRoot 'wo24-qualification.ps1') -Mode Compare -Before $before -After $after -RequireReboot -OutputFile (Join-Path $out 'reboot-not-observed.json');throw 'SAME_BOOT_INCORRECTLY_ACCEPTED'}catch{if($_.Exception.Message -notmatch 'WO24_COMPARISON_REVIEW:10'){throw}}
$records|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'vault-and-apply.json')
@{Source=$source;Status='PASS';Scope='Windows synthetic capture and comparator; not real installation, UAC, independent SID, GUI or reboot';ExitCode=0}|ConvertTo-Json|Set-Content (Join-Path $out 'summary.json')

if($env:GITHUB_ACTIONS -eq 'true' -and $env:RUNNER_ENVIRONMENT -eq 'github-hosted'){
 $root=Join-Path $env:RUNNER_TEMP ('wo20-wo24-strict-'+[guid]::NewGuid().ToString('N'))
 & $probe prepare-many $root|Out-Null
 if($LASTEXITCODE -ne 0){throw 'STRICT_FIXTURE_FAILED'}
 & (Join-Path $repo 'tools/SmartPerformanceDoctor.WO24Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO24Probe.exe') strict-apply $root | Set-Content (Join-Path $out 'strict-performance.json')
 if($LASTEXITCODE -ne 0){throw 'STRICT_APPLY_FAILED'}
}
