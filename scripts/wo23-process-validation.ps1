param([string]$OutputDirectory='artifacts/wo23-process')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$wo23Output=Join-Path $repo $OutputDirectory;New-Item -ItemType Directory -Force $wo23Output|Out-Null
. (Join-Path $PSScriptRoot 'wo20-process-validation.ps1') -OutputDirectory ($OutputDirectory+'/harness') -FunctionsOnly
$script:wo20Records.Clear()
try{
 foreach($point in @('AfterFinalHashVerification','AttestationCreated','AttestationPartialWrite','AttestationFlushed','InstallationPointerCreated','InstallationPointerFlushed','BeforeInstallationPointerReplace','AfterInstallationPointerReplace','BeforeCommittedJournal','AfterCommittedJournal','BeforeSuccessReturn')){
  $root=Join-Path $parent ('wo20-wo23-commit-'+[guid]::NewGuid().ToString('N'))
  Invoke-Probe prepare $root;Invoke-Probe vault-check $root;Kill-Probe ('commit-kill:'+$point) $root
  Invoke-Probe publication-query $root;$state=$script:wo20Records[-1].Output|ConvertFrom-Json
  $complete=$point -in @('AfterCommittedJournal','BeforeSuccessReturn')
  if($state.Trust.Verified -ne $complete){throw "INCORRECT_PUBLICATION_STATE:$point"}
  if(!$complete){Invoke-Probe recover $root;Invoke-Probe verify-original $root}
  Invoke-Probe vault-check $root
 }
 'WO23_COMMIT_PROCESS_TERMINATION=PASS; POWER_LOSS=NOT_TESTED'
}finally{$script:wo20Records|ConvertTo-Json -Depth 14|Set-Content (Join-Path $wo23Output 'process-results.json')}
