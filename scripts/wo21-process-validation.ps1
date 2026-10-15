param([string]$OutputDirectory='artifacts/wo21-ci')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$wo21Output=Join-Path $repo $OutputDirectory
New-Item -ItemType Directory -Force $wo21Output | Out-Null
. (Join-Path $PSScriptRoot 'wo20-process-validation.ps1') -OutputDirectory ($OutputDirectory+'/wo20')
try{
 foreach($point in @('Normal','AuthorizationVerified','AfterPromotion','AfterFileRestore','AfterRestoredJournal')){
  $root=Join-Path $parent ('wo20-wo21-approval-'+[guid]::NewGuid().ToString('N'))
  Invoke-Probe prepare $root;Invoke-Probe vault-check $root;Invoke-Probe legacy-prepare $root;Invoke-Probe legacy-request $root
  if($point -eq 'Normal'){Invoke-Probe approved $root}
  else{
   Kill-Probe ('approved-kill:'+$point) $root
   if($point -eq 'AuthorizationVerified'){Invoke-Probe legacy-request $root;Invoke-Probe approved $root}
   else{Invoke-Probe recover $root}
  }
  Invoke-Probe verify-original $root;Invoke-Probe vault-recovery-check $root
 }
 $root=Join-Path $parent ('wo20-wo21-unknown-'+[guid]::NewGuid().ToString('N'))
 Invoke-Probe prepare $root;Invoke-Probe legacy-prepare $root;Invoke-Probe legacy-unknown $root;Invoke-Probe vault-check $root
 $root=Join-Path $parent ('wo20-wo21-mismatch-'+[guid]::NewGuid().ToString('N'))
 Invoke-Probe prepare $root;Invoke-Probe apply $root;Invoke-Probe attestation-mismatch $root;Invoke-Probe vault-recovery-check $root
 $root=Join-Path $parent ('wo20-wo21-enrollment-'+[guid]::NewGuid().ToString('N'))
 Invoke-Probe prepare $root;Invoke-Probe enroll $root;Invoke-Probe vault-recovery-check $root
 'WO21_SYNTHETIC_RECOVERY=PASS; PRIVILEGED_E2E=NOT_TESTED; GUI=NOT_TESTED; REBOOT=NOT_TESTED'
}finally{$script:wo20Records|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $wo21Output 'process-results.json') -Encoding UTF8}
