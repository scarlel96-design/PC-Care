param([Parameter(Mandatory=$true)][guid]$VMId,[Parameter(Mandatory=$true)][guid]$CheckpointId,[Parameter(Mandatory=$true)][string]$OutputFile,[switch]$ExecuteRestore)
$ErrorActionPreference='Stop'
if(!(Get-Command Get-VM -ErrorAction SilentlyContinue)){throw 'VM_UNAVAILABLE'}
if(Test-Path -LiteralPath $OutputFile){throw 'NEW_OUTPUT_REQUIRED'}
$vm=Get-VM -Id $VMId
$checkpoint=Get-VMSnapshot -VM $vm|Where-Object Id -eq $CheckpointId
if(@($checkpoint).Count -ne 1){throw 'CHECKPOINT_NOT_OBSERVED'}
if($ExecuteRestore){
 if(!$vm.Name.StartsWith('PC-Care-WO24-',[StringComparison]::Ordinal)){throw 'DEDICATED_VM_NAME_REQUIRED'}
 Restore-VMSnapshot -VMSnapshot $checkpoint -Confirm:$false
 $vm=Get-VM -Id $VMId
}
@{Status='OBSERVED';Command='Get-VM -Id <VM_ID>; Get-VMSnapshot -VM <OBSERVED_VM>';VmId=$vm.Id;CheckpointId=$checkpoint.Id;Created=$checkpoint.CreationTime;Captured=[DateTimeOffset]::UtcNow;RestoreExecuted=$ExecuteRestore.IsPresent;GuestBytesVerified=$false;Scope='Local hypervisor observation; guest transport is not remote attestation'}|ConvertTo-Json|Set-Content -LiteralPath $OutputFile
