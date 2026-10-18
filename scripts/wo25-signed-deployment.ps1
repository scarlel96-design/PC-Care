param([string]$BinaryDirectory,[string]$GuestRoot,[string]$ExpectedGuestUuid,[string]$ExpectedSid,
 [string]$WindowsProbe,[string]$BinaryManifest,[string]$BinarySignature,[string]$BinaryPublicKey,[string]$BinaryKeyHash,
 [string]$ProbeManifest,[string]$ProbeSignature,[string]$ProbePublicKey,[string]$ProbeKeyHash,[switch]$Execute)
$ErrorActionPreference='Stop'
if(!$Execute){@{Status='PREPARED';ActualDeployment=$false;Requirement='Disposable guest; observed checkpoint; protected source-bound inputs; independently pinned public keys for both complete input closures'}|ConvertTo-Json;return}
# Authenticate all input bytes before starting the verifier or guest signer.
. (Join-Path $PSScriptRoot 'wo25-input-closure.ps1')
Assert-Wo26RuntimeEnvironment
Assert-Wo26RuntimeEnvironment
$binary=$null;$probe=$null
try {
 $binary=Open-Wo25InputClosure $BinaryDirectory $BinaryManifest $BinarySignature $BinaryPublicKey $BinaryKeyHash
 $probe=Open-Wo25InputClosure (Split-Path $WindowsProbe -Parent) $ProbeManifest $ProbeSignature $ProbePublicKey $ProbeKeyHash
 foreach($directory in @($binary.Root,$probe.Root) + @($binary.Manifest.Files.Path | ForEach-Object {Split-Path (Join-Path $binary.Root $_) -Parent}) + @($probe.Manifest.Files.Path | ForEach-Object {Split-Path (Join-Path $probe.Root $_) -Parent})){
  $acl=Get-Acl -LiteralPath $directory
  $owner=([Security.Principal.NTAccount]::new($acl.Owner)).Translate([Security.Principal.SecurityIdentifier]).Value
  if($owner -notin 'S-1-5-18','S-1-5-32-544'){throw 'DEPLOYMENT_PROTECTED_OWNER_REQUIRED'}
  $writes=[Security.AccessControl.FileSystemRights]::Write -bor [Security.AccessControl.FileSystemRights]::Delete -bor [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor [Security.AccessControl.FileSystemRights]::ChangePermissions -bor [Security.AccessControl.FileSystemRights]::TakeOwnership
  foreach($ace in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])){
   if($ace.AccessControlType -eq 'Allow' -and ($ace.FileSystemRights -band $writes) -and $ace.IdentityReference.Value -notin 'S-1-5-18','S-1-5-32-544'){throw 'DEPLOYMENT_WRITABLE_INPUT_ROOT'}
  }
 }
 foreach($entry in $probe.Manifest.Files){
  if(!$entry.IsPe){continue}
  $expected=if($entry.Publisher){$entry.Publisher}else{$probe.Manifest.Publisher}
  $native=Get-AuthenticodeSignature -LiteralPath (Join-Path $probe.Root $entry.Path)
  if($native.Status -ne 'Valid' -or $native.SignerCertificate.Thumbprint -ne $expected){throw 'VERIFIER_DEPENDENCY_SIGNATURE_REJECTED'}
 }
 foreach($name in @('SmartPerformanceDoctor.UpdateBridge.exe','SmartPerformanceDoctor.UpdateBridge.dll','SmartPerformanceDoctor.UpdateEngine.dll','SmartPerformanceDoctor.UpdateHelper.exe','SmartPerformanceDoctor.UpdateHelper.dll','SmartPerformanceDoctor.UpdateBridge.deps.json','SmartPerformanceDoctor.UpdateBridge.runtimeconfig.json','SmartPerformanceDoctor.UpdateHelper.deps.json','SmartPerformanceDoctor.UpdateHelper.runtimeconfig.json')){
  if($name -notin $binary.Manifest.Files.Path){throw "DEPLOYMENT_REQUIRED_INPUT_MISSING:$name"}
 }
 $ExpectedProbeHash=(Get-FileHash -LiteralPath $WindowsProbe).Hash
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
foreach($item in $binary.Manifest.Files){
 if($item.Path -in $signing.File){continue}
 $destination=Join-Path $GuestRoot $item.Path
 [IO.Directory]::CreateDirectory((Split-Path $destination -Parent))|Out-Null
 Copy-Item -LiteralPath (Join-Path $BinaryDirectory $item.Path) -Destination $destination
 if((Get-FileHash -LiteralPath $destination).Hash -ne $item.Sha256){throw 'DEPLOYED_DEPENDENCY_CHANGED'}
}
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
 $dependencyRecords=foreach($entry in $binary.Manifest.Files){
  if(!$entry.IsPe -or $entry.Path -in $signing.File){continue}
  $expectedPublisher=if($entry.Publisher){$entry.Publisher}else{$binary.Manifest.Publisher}
  $raw=& $WindowsProbe verify (Join-Path $GuestRoot $entry.Path);$code=$LASTEXITCODE
  if($code -ne 0){throw 'DEPENDENCY_VERIFIER_FAILED'}
  $result=$raw|ConvertFrom-Json
  if($result.Code -ne 0 -or $result.Thumbprint -ne $expectedPublisher -or $result.Hash -ne $entry.Sha256){throw 'DEPENDENCY_PUBLISHER_REJECTED'}
  @{File=$entry.Path;Identity=$result.Identity;Hash=$result.Hash;SignatureVerified=$true}
 }
 $process=Start-Process -FilePath (Join-Path $GuestRoot 'SmartPerformanceDoctor.UpdateBridge.exe') -ArgumentList '--qualification-unsupported' -WindowStyle Hidden -PassThru -Wait
 if($process.ExitCode -ne 2){throw 'BRIDGE_STARTUP_CONTRACT_FAILED'}
 @{Status='PARTIAL';ProtectedGuestDeployment=$true;BridgeStartupExit=$process.ExitCode;Files=$records;Dependencies=$dependencyRecords;ActualTransition='NOT TESTED';Reason='Unsupported-verb startup does not execute bootstrap transition. Production publisher policy remains empty.'}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $GuestRoot 'deployment-result.json')
}finally{
 $entry='Cert:\CurrentUser\Root\'+$thumb
 if(Test-Path -LiteralPath $entry){Remove-Item -LiteralPath $entry -ErrorAction Stop}
 if(Test-Path -LiteralPath $entry){throw 'TEST_CERTIFICATE_REMAINS'}
}

} finally {foreach($binding in @($probe,$binary)){if($binding){foreach($stream in $binding.Leases){$stream.Dispose()}}}}
