param([string]$OutputDirectory='artifacts/wo22-ci')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$wo22Output=Join-Path $repo $OutputDirectory
New-Item -ItemType Directory -Force $wo22Output | Out-Null
. (Join-Path $PSScriptRoot 'wo21-process-validation.ps1') -OutputDirectory ($OutputDirectory+'/wo21')
$script:wo20Records.Clear()
try{
 foreach($point in @('RequestVerified','BeforeOperatorConfirmation','AfterOperatorConfirmation','ApprovalCreated','ApprovalFlushed','BeforePromotion','AfterPromotion','AfterFileRestore','AfterLastFileRestore','BeforeRestoredJournal','AfterRestoredJournal','BeforeResultReturn')){
  $root=Join-Path $parent ('wo20-wo22-approval-'+[guid]::NewGuid().ToString('N'))
  Invoke-Probe prepare $root;Invoke-Probe legacy-prepare $root;Invoke-Probe legacy-request $root
  Kill-Probe ('approved-kill:'+$point) $root
  Invoke-Probe approval-query $root
  $state=($script:wo20Records[-1].Output|ConvertFrom-Json)
  $committed=$point -in @('AfterRestoredJournal','BeforeResultReturn')
  if($state.FilesVerified -ne $committed){throw "Incorrect completion observation: $point"}
  if($point -in @('RequestVerified','BeforeOperatorConfirmation','AfterOperatorConfirmation')){
   if($state.ApprovalConsumed){throw 'Unreserved approval marked consumed'}
   Invoke-Probe approved $root
  }elseif($point -eq 'ApprovalCreated'){
   if($state.State -ne 5){throw 'Empty reservation not marked review required'}
   Invoke-Probe legacy-request $root;Invoke-Probe approved $root
  }elseif($point -in @('ApprovalFlushed','BeforePromotion')){
   Invoke-Probe approval-replay $root
   Invoke-Probe legacy-request $root;Invoke-Probe approved $root
  }elseif(!$committed){Invoke-Probe recover $root}
  Invoke-Probe approval-query $root
  if(!(($script:wo20Records[-1].Output|ConvertFrom-Json).FilesVerified)){throw 'Final restored bytes not qualified'}
  Invoke-Probe approval-replay $root;Invoke-Probe verify-original $root;Invoke-Probe vault-check $root
 }
 foreach($point in @('PreparedRecordCreated','RecordFlushed','BeforeActivation','Activated')){
  $root=Join-Path $parent ('wo20-wo22-enroll-'+[guid]::NewGuid().ToString('N'))
  Invoke-Probe prepare $root;Kill-Probe ('enroll-kill:'+$point) $root;Invoke-Probe trust-query $root
  $state=$script:wo20Records[-1].Output|ConvertFrom-Json
  if($state.Verified -ne ($point -eq 'Activated')){throw 'Enrollment completion incorrectly classified'}
  Invoke-Probe verify-original $root;Invoke-Probe vault-check $root
 }
 foreach($point in @('AfterPackageVerification','DuringStageExtraction','DuringBackupCreation','BeforeReplacement','AfterFirstReplacement','BeforeCommittedJournal')){
  $root=Join-Path $parent ('wo20-wo22-cancel-'+[guid]::NewGuid().ToString('N'))
  Invoke-Probe prepare $root;Invoke-Probe ('cancel:'+$point) $root;Invoke-Probe vault-check $root
 }
 'WO22_RECONCILIATION=PASS; SIGNED_ADMIN_E2E=NOT_TESTED; GUI=NOT_TESTED; REBOOT=NOT_TESTED'
}finally{$script:wo20Records|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $wo22Output 'process-results.json') -Encoding UTF8}
