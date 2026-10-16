param([string]$OutputDirectory='artifacts/wo23-security')
$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows'){throw 'DISPOSABLE_GITHUB_HOSTED_WINDOWS_REQUIRED'}
$repo=Split-Path $PSScriptRoot -Parent
$out=Join-Path $repo $OutputDirectory;New-Item -ItemType Directory -Force $out | Out-Null
$source=(& git -C $repo rev-parse HEAD).Trim()
function Mark([string]$Stage){
 Write-Host ("WO23_SECURITY:"+$Stage)
 @{Stage=$Stage;Time=[DateTimeOffset]::UtcNow;Source=$source}|ConvertTo-Json|Set-Content (Join-Path $out 'security-progress.json')
}
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$admin=([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$envRecord=@{Source=$source;OS=[Environment]::OSVersion.VersionString;Runner=$env:RUNNER_ENVIRONMENT;Administrator=$admin;SID='REDACTED';FileSystem=(Get-Volume -DriveLetter ([IO.Path]::GetPathRoot($env:RUNNER_TEMP).Substring(0,1))).FileSystem;WinVerifyTrust='wintrust.dll';CertificateProvider=[bool](Get-Command New-SelfSignedCertificate -ErrorAction SilentlyContinue);SigningTool='Set-AuthenticodeSignature';InteractiveUAC='NOT TESTED';IndependentSID='NOT TESTED';Reboot='NOT TESTED';RestrictedToken='NOT TESTED'}
$envRecord|ConvertTo-Json|Set-Content (Join-Path $out 'environment.json')
$probe=Join-Path $repo 'tools/SmartPerformanceDoctor.WO23WindowsProbe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO23WindowsProbe.exe'
$aclRoot=Join-Path $env:RUNNER_TEMP ('wo23-acl-'+[guid]::NewGuid().ToString('N'))
Mark 'AclStart'
$acl=& $probe acl $aclRoot
$aclExit=$LASTEXITCODE
@{Command='WO23WindowsProbe acl <CI_TEMP>/wo23-acl';Input='Actual NTFS ACL and file handle operations';Source=$source;ExitCode=$aclExit;Output=($acl|Out-String)|ConvertFrom-Json}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $out 'acl.json')
if($aclExit -ne 0){throw 'ACTUAL_ACL_QUALIFICATION_FAILED'}
Mark 'AclCompleted'
$signRoot=Join-Path $env:RUNNER_TEMP ('wo23-sign-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $signRoot|Out-Null
$names=@('SmartPerformanceDoctor.UpdateBridge.exe','SmartPerformanceDoctor.UpdateBridge.dll','SmartPerformanceDoctor.UpdateEngine.dll','SmartPerformanceDoctor.UpdateHelper.exe','SmartPerformanceDoctor.UpdateHelper.dll')
$records=[Collections.Generic.List[object]]::new();$certs=[Collections.Generic.List[object]]::new()
function Verify([string]$Name,[string]$Path,[bool]$Accepted,[string]$Thumbprint){
 Mark ('VerifyStart:'+ $Name)
 $raw=& $probe verify $Path;$code=$LASTEXITCODE;$result=($raw|Out-String)|ConvertFrom-Json
 $observed=$result.Code -eq 0 -and $result.Thumbprint -eq $Thumbprint
 Mark ('VerifyCompleted:'+ $Name)
 $records.Add(@{Name=$Name;Command='WO23WindowsProbe verify <CI_SYNTHETIC_PE>';Input=(Split-Path $Path -Leaf);ExitCode=$code;Output=$result;ExpectedAccepted=$Accepted;Accepted=$observed;Status=$(if($observed -eq $Accepted -and $code -eq 0){'PASS'}else{'FAIL'})})
 if($code -ne 0 -or $observed -ne $Accepted -or $result.ProductionAccepted){throw "AUTHENTICODE_ASSERTION:$Name"}
}
try{
 foreach($subject in @('PC-Care WO23 Disposable CI','PC-Care WO23 Other Publisher','PC-Care WO23 Untrusted Publisher')){
  Mark ('CertificateCreate:'+ $subject)
  $cert=New-SelfSignedCertificate -Type CodeSigningCert -Subject ('CN='+$subject) -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy NonExportable -Provider 'Microsoft Software Key Storage Provider' -KeyAlgorithm RSA -KeyLength 2048 -NotAfter (Get-Date).AddDays(2)
  $certs.Add($cert)
  Mark ('CertificateCreated:'+ $subject)
  $public=Join-Path $signRoot ($cert.Thumbprint+'.cer');Export-Certificate -Cert $cert -FilePath $public|Out-Null
  if($certs.Count -lt 3){Mark 'TemporaryCiMachineRootImport';Import-Certificate -FilePath $public -CertStoreLocation Cert:\LocalMachine\Root|Out-Null;Mark 'TemporaryCiMachineRootImported'}
 }
 foreach($name in $names){
  $project=if($name -like '*UpdateHelper*'){'src/SmartPerformanceDoctor.UpdateHelper'}else{'tools/SmartPerformanceDoctor.UpdateBridge'}
  $from=Join-Path $repo ($project+'/bin/x64/Release/net10.0-windows10.0.26100.0/'+$name)
  $path=Join-Path $signRoot $name;Copy-Item -LiteralPath $from -Destination $path
  Verify ('UnsignedRejected:'+ $name) $path $false $certs[0].Thumbprint
  Mark ('SignStart:'+ $name)
  $signed=Set-AuthenticodeSignature -FilePath $path -Certificate $certs[0] -HashAlgorithm SHA256
  Mark ('SignCompleted:'+ $name)
  if($signed.Status -ne 'Valid'){throw ('SIGNING_FAILED:'+ $signed.Status)}
  Verify ('SignedAccepted:'+ $name) $path $true $certs[0].Thumbprint
 }
 $engine=Join-Path $signRoot 'SmartPerformanceDoctor.UpdateEngine.dll'
 $pinned=((& $probe verify-pinned $engine)|Out-String)|ConvertFrom-Json
 if($pinned.Code -ne 0 -or $pinned.BlockedMutations -ne 2){throw 'SIGNED_HANDLE_REPLACEMENT_NOT_BLOCKED'}
 $records.Add(@{Name='SignedFinalHandleRejectsDeletionAndMutation';Status='PASS';Command='WO23WindowsProbe verify-pinned <CI_SYNTHETIC_PE>';ExitCode=$LASTEXITCODE;Output=$pinned})
 $before=((& $probe verify $engine)|Out-String)|ConvertFrom-Json
 Copy-Item -LiteralPath $engine -Destination (Join-Path $signRoot 'replacement.dll')
 $after=((& $probe verify (Join-Path $signRoot 'replacement.dll'))|Out-String)|ConvertFrom-Json
 if($before.Identity -eq $after.Identity -or $before.Hash -ne $after.Hash){throw 'FILE_ID_COPY_BINDING_FAILED'}
 $records.Add(@{Name='SignedDllCopyHasDifferentFileIdentity';Status='PASS';ExitCode=0;Output=@{SameHash=$true;DifferentIdentity=$true}})
 $other=Join-Path $signRoot 'other.dll';Copy-Item -LiteralPath $engine -Destination $other
 Set-AuthenticodeSignature -FilePath $other -Certificate $certs[1] -HashAlgorithm SHA256|Out-Null
 Verify 'ValidOtherPublisherRejectedByExpectedPin' $other $false $certs[0].Thumbprint
 $edited=Join-Path $signRoot 'edited.dll';Copy-Item -LiteralPath $engine -Destination $edited
 $stream=[IO.File]::Open($edited,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite)
 try{$stream.Position=512;$value=$stream.ReadByte();$stream.Position=512;$stream.WriteByte($value -bxor 1);$stream.Flush($true)}finally{$stream.Dispose()}
 Verify 'PostSigningModificationRejected' $edited $false $certs[0].Thumbprint
 $untrusted=Join-Path $signRoot 'untrusted.dll';Copy-Item -LiteralPath $engine -Destination $untrusted
 Set-AuthenticodeSignature -FilePath $untrusted -Certificate $certs[2] -HashAlgorithm SHA256|Out-Null
 Verify 'UntrustedChainRejected' $untrusted $false $certs[2].Thumbprint
}finally{
 foreach($cert in $certs){
  if(Test-Path -LiteralPath ('Cert:\LocalMachine\Root\'+$cert.Thumbprint)){Remove-Item -LiteralPath ('Cert:\LocalMachine\Root\'+$cert.Thumbprint) -ErrorAction Stop}
  if(Test-Path -LiteralPath ('Cert:\CurrentUser\My\'+$cert.Thumbprint)){Remove-Item -LiteralPath ('Cert:\CurrentUser\My\'+$cert.Thumbprint) -DeleteKey -ErrorAction Stop}
 }
 $remaining=@($certs|Where-Object{(Test-Path -LiteralPath ('Cert:\CurrentUser\My\'+$_.Thumbprint)) -or (Test-Path -LiteralPath ('Cert:\LocalMachine\Root\'+$_.Thumbprint))})
 @{Source=$source;Status=$(if($records.Count -ge 14 -and !($records|Where-Object Status -eq 'FAIL') -and $remaining.Count -eq 0){'PASS'}else{'PARTIAL'});Scope='Actual PE Authenticode in disposable GitHub-hosted Windows; test pin only; production pins unchanged';Tests=$records;CertificateCleanupVerified=$remaining.Count -eq 0;TrustStore='Disposable CI LocalMachine Root; own entries removed in finally';Timestamp='NOT TESTED';ExpiredCertificate='NOT TESTED';UAC='NOT TESTED'}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $out 'authenticode.json')
}
'WO23_ACTUAL_WINDOWS_ACL_AND_AUTHENTICODE=PASS'
