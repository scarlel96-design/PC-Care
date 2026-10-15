param([Parameter(Mandatory=$true)][string]$BinaryDirectory,
 [Parameter(Mandatory=$true)][string]$GuestTestRoot,
 [Parameter(Mandatory=$true)][string]$ExpectedGuestUuid,
 [Parameter(Mandatory=$true)][string]$ExpectedTestSid,
 [switch]$Execute)
$ErrorActionPreference='Stop'
$names=@('SmartPerformanceDoctor.UpdateBridge.exe','SmartPerformanceDoctor.UpdateBridge.dll','SmartPerformanceDoctor.UpdateEngine.dll','SmartPerformanceDoctor.UpdateHelper.exe','SmartPerformanceDoctor.UpdateHelper.dll')
if(!$Execute){
 [ordered]@{Status='PREPARED';Files=$names;Requirement='Verified disposable VM checkpoint and independent test SID';TrustStore='Guest CurrentUser only';ProductionRoot='UNCHANGED';Timestamp='NOT TESTED'}|ConvertTo-Json
 return
}
$uuid=(Get-CimInstance Win32_ComputerSystemProduct).UUID
$model=(Get-CimInstance Win32_ComputerSystem).Model
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
if($uuid -ne $ExpectedGuestUuid -or $sid -ne $ExpectedTestSid -or $model -notmatch 'Virtual|VMware|VirtualBox'){throw 'GUEST_IDENTITY_MISMATCH'}
if(![IO.Path]::IsPathFullyQualified($GuestTestRoot) -or !(Split-Path $GuestTestRoot -Leaf).StartsWith('wo21-') -or (Test-Path $GuestTestRoot)){throw 'NEW_GUEST_TEST_ROOT_REQUIRED'}
$source=[IO.Path]::GetFullPath($BinaryDirectory)
foreach($name in $names){if(!(Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)){throw "MISSING_PE:$name"}}
New-Item -ItemType Directory $GuestTestRoot | Out-Null
foreach($name in $names){Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $GuestTestRoot $name)}
$cert=$null
try{
 $cert=New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=PC-Care WO21 Disposable Guest Only' -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddDays(2)
 $public=Join-Path $GuestTestRoot 'test-publisher.cer'
 Export-Certificate -Cert $cert -FilePath $public | Out-Null
 Import-Certificate -FilePath $public -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
 $results=foreach($name in $names){
  $path=Join-Path $GuestTestRoot $name
  $signed=Set-AuthenticodeSignature -FilePath $path -Certificate $cert -HashAlgorithm SHA256
  $verified=Get-AuthenticodeSignature -LiteralPath $path
  if($signed.Status -ne 'Valid' -or $verified.Status -ne 'Valid' -or $verified.SignerCertificate.Thumbprint -ne $cert.Thumbprint){throw "TEST_SIGNATURE_REJECTED:$name"}
  @{File=$name;Status=[string]$verified.Status;Thumbprint=$cert.Thumbprint;Sha256=(Get-FileHash $path).Hash}
 }
 $results|ConvertTo-Json|Set-Content (Join-Path $GuestTestRoot 'signed-pe-results.json')
 'GUEST_PE_SIGNING_ONLY: production publisher validation and transition E2E require the isolated build profile and separate tests'
}finally{
 if($cert){
  Remove-Item -LiteralPath ('Cert:\CurrentUser\Root\'+$cert.Thumbprint) -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath ('Cert:\CurrentUser\My\'+$cert.Thumbprint) -ErrorAction SilentlyContinue
 }
}
