param([Parameter(Mandatory=$true)][string]$BinaryDirectory,
 [Parameter(Mandatory=$true)][string]$GuestRoot,[Parameter(Mandatory=$true)][string]$ExpectedGuestUuid,
 [Parameter(Mandatory=$true)][string]$ExpectedSid,[Parameter(Mandatory=$true)][string]$WindowsProbe,
 [Parameter(Mandatory=$true)][string]$ExpectedProbeHash,[switch]$Execute)
$ErrorActionPreference='Stop'
if(!$Execute){
 @{Status='PREPARED';ActualDeployment=$false;ActualTransition=$false;Requirement='Verified disposable VM and checkpoint; administrator test SID; five PE inputs and runtime JSON; source-bound Windows probe';CertificateScope='Guest CurrentUser only';ProductionTrust='UNCHANGED'}|ConvertTo-Json
 return
}
$uuid=(Get-CimInstance Win32_ComputerSystemProduct).UUID
$model=(Get-CimInstance Win32_ComputerSystem).Model
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if($uuid -ne $ExpectedGuestUuid -or $identity.User.Value -ne $ExpectedSid -or $model -notmatch 'Virtual|VMware|VirtualBox'){throw 'GUEST_IDENTITY_MISMATCH'}
if(!([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'GUEST_ADMIN_REQUIRED'}
if((Get-FileHash -LiteralPath $WindowsProbe).Hash -ne $ExpectedProbeHash){throw 'PROBE_BINARY_CHANGED'}
foreach($name in @('SmartPerformanceDoctor.UpdateBridge.runtimeconfig.json','SmartPerformanceDoctor.UpdateBridge.deps.json','SmartPerformanceDoctor.UpdateHelper.runtimeconfig.json','SmartPerformanceDoctor.UpdateHelper.deps.json')){
 if(!(Test-Path -LiteralPath (Join-Path $BinaryDirectory $name))){throw "RUNTIME_INPUT_MISSING:$name"}
}
# Reuse the guarded guest signer. It removes its own private key before returning.
& (Join-Path $PSScriptRoot 'wo21-test-signing.ps1') -BinaryDirectory $BinaryDirectory -GuestTestRoot $GuestRoot -ExpectedGuestUuid $ExpectedGuestUuid -ExpectedTestSid $ExpectedSid -Execute
$signing=Get-Content -LiteralPath (Join-Path $GuestRoot 'signed-pe-results.json') -Raw|ConvertFrom-Json
$thumb=$signing[0].Thumbprint
foreach($name in @('SmartPerformanceDoctor.UpdateBridge.runtimeconfig.json','SmartPerformanceDoctor.UpdateBridge.deps.json','SmartPerformanceDoctor.UpdateHelper.runtimeconfig.json','SmartPerformanceDoctor.UpdateHelper.deps.json')){Copy-Item -LiteralPath (Join-Path $BinaryDirectory $name) -Destination (Join-Path $GuestRoot $name)}
$admins=[Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
$acl=[Security.AccessControl.DirectorySecurity]::new();$acl.SetAccessRuleProtection($true,$false);$acl.SetOwner($admins)
foreach($sid in @($admins,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'),'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow'))
[IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($GuestRoot),$acl)
try{
 Import-Certificate -FilePath (Join-Path $GuestRoot 'test-publisher.cer') -CertStoreLocation Cert:\CurrentUser\Root|Out-Null
 $records=foreach($entry in $signing){
  $file=Join-Path $GuestRoot $entry.File
  if((Get-FileHash -LiteralPath $file).Hash -ne $entry.Sha256){throw 'DEPLOYED_FILE_CHANGED'}
  $raw=& $WindowsProbe verify $file;$code=$LASTEXITCODE
  if($code -ne 0){throw 'WINDOWS_PROBE_FAILED'}
  $result=$raw|ConvertFrom-Json
  if($result.Code -ne 0 -or $result.Thumbprint -ne $thumb -or $result.ProductionAccepted){throw 'DEPLOYMENT_TRUST_REJECTED'}
  @{File=$entry.File;Identity=$result.Identity;Hash=$result.Hash;SignatureVerified=$true;ProductionAccepted=$false}
 }
 $process=Start-Process -FilePath (Join-Path $GuestRoot 'SmartPerformanceDoctor.UpdateBridge.exe') -ArgumentList '--qualification-unsupported' -WindowStyle Hidden -PassThru -Wait
 if($process.ExitCode -ne 2){throw 'BRIDGE_STARTUP_CONTRACT_FAILED'}
 @{Status='PARTIAL';ProtectedGuestDeployment=$true;BridgeStartupExit=$process.ExitCode;Files=$records;ActualTransition='NOT TESTED';Reason='Unsupported-verb startup does not execute bootstrap transition. Production publisher policy remains empty.'}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $GuestRoot 'deployment-result.json')
}finally{
 $entry='Cert:\CurrentUser\Root\'+$thumb
 if(Test-Path -LiteralPath $entry){Remove-Item -LiteralPath $entry -ErrorAction Stop}
 if(Test-Path -LiteralPath $entry){throw 'TEST_CERTIFICATE_REMAINS'}
}
