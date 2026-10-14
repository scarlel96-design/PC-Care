param([string]$VmName,[string]$SnapshotId,[string]$OriginalUserSid,[string]$OutputFile)
$ErrorActionPreference='Stop'
$status='NOT TESTED';$reason='No verified disposable VM and independent SID were supplied.'
$hyperVAvailable=$null-ne(Get-Command Get-VM -ErrorAction SilentlyContinue)
$snapshotVerified=$false;$independentSidVerified=$false
if($hyperVAvailable -and $VmName -and $SnapshotId){
    $vm=Get-VM -Name $VmName -ErrorAction Stop
    $snapshot=Get-VMSnapshot -VM $vm | Where-Object { $_.Id.ToString()-eq $SnapshotId }
    $snapshotVerified=$null-ne $snapshot
    $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $independentSidVerified=$OriginalUserSid -and $sid-ne $OriginalUserSid
    if($snapshotVerified -and $independentSidVerified){$status='PARTIAL';$reason='Prerequisites verified. Run GuiProbe under this independent profile and record interactions.'}
}
$result=[pscustomobject]@{Status=$status;GuiExecuted=$false;HyperVCmdletsAvailable=$hyperVAvailable;
    SnapshotVerified=$snapshotVerified;IndependentSidVerified=[bool]$independentSidVerified;Reason=$reason;
    Next='Verify a disposable snapshot and distinct SID. Use isolated AppData and synthetic data. Run existing GuiProbe storage and vault paths.'}
$json=$result|ConvertTo-Json
if($OutputFile){$json|Set-Content -LiteralPath $OutputFile -Encoding utf8}
Write-Output $json
