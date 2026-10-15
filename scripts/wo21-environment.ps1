param([string]$OutputDirectory='artifacts/wo21-environment')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$output=Join-Path $repo $OutputDirectory
New-Item -ItemType Directory -Force $output | Out-Null
$features=@()
foreach($name in @('Containers-DisposableClientVM','Microsoft-Hyper-V-All')){
 try{$feature=Get-WindowsOptionalFeature -Online -FeatureName $name -ErrorAction Stop;$features+=@{Name=$name;State=[string]$feature.State}}
 catch{$features+=@{Name=$name;State='UNVERIFIED';Error=$_.Exception.GetType().Name}}
}
$vms=@();$snapshots=@()
if(Get-Command Get-VM -ErrorAction SilentlyContinue){
 try{$vms=@(Get-VM|Select-Object Name,State,Generation);$snapshots=@(Get-VMSnapshot -VMName *|Select-Object VMName,Name,CreationTime)}catch{}
}
$inventory=[ordered]@{
 Command='pwsh -NoProfile -File scripts/wo21-environment.ps1';Input='Read-only host inventory';Source=(& git -C $repo rev-parse HEAD).Trim()
 OS=[Environment]::OSVersion.VersionString;Processors=[Environment]::ProcessorCount
 HyperVCommand=[bool](Get-Command Get-VM -ErrorAction SilentlyContinue)
 VMwareCommand=[bool](Get-Command vmrun -ErrorAction SilentlyContinue)
 VirtualBoxCommand=[bool](Get-Command VBoxManage -ErrorAction SilentlyContinue)
 SandboxExecutable=(Test-Path "$env:windir\System32\WindowsSandbox.exe");Features=$features
 VMs=$vms;Snapshots=$snapshots;RemoteHost='NOT_CONFIGURED_IN_QUALIFICATION'
 IsolationStatus=$(if($vms.Count -and $snapshots.Count){'REQUIRES_SELECTION_AND_SID_VERIFICATION'}else{'VM_UNAVAILABLE'})
 InteractiveUac='INTERACTIVE_UAC_UNAVAILABLE';TestCertificate='TEST_CERTIFICATE_UNAVAILABLE'
 Gui='NOT TESTED';Reboot='NOT TESTED';PhysicalPowerLoss='NOT TESTED';Daybreak='NO_PLATFORM_CHALLENGE_OBSERVED'
 ExitCode=0
}
$inventory|ConvertTo-Json -Depth 7|Set-Content (Join-Path $output 'environment.json')
$inventory|ConvertTo-Json -Depth 7
