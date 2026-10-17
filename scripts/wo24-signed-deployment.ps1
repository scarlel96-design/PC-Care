param([Parameter(Mandatory=$true)][string]$BinaryDirectory,
 [Parameter(Mandatory=$true)][string]$GuestRoot,[Parameter(Mandatory=$true)][string]$ExpectedGuestUuid,
 [Parameter(Mandatory=$true)][string]$ExpectedSid,[Parameter(Mandatory=$true)][string]$WindowsProbe,
 [Parameter(Mandatory=$true)][string]$ExpectedProbeHash,[switch]$Execute)
$ErrorActionPreference='Stop'
if(!$Execute){
 @{Status='PREPARED';ActualDeployment=$false;ActualTransition=$false;Requirement='Verified disposable VM and checkpoint; administrator test SID; five PE inputs and runtime JSON; source-bound Windows probe';CertificateScope='Guest CurrentUser only';ProductionTrust='UNCHANGED'}|ConvertTo-Json
 return
}
throw 'WO25_AUTHENTICATED_INPUT_CLOSURE_REQUIRED: use wo25-signed-deployment.ps1 with both signed input manifests'
