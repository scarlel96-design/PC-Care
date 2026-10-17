param([ValidateSet('Prepare','Capture','Compare')][string]$Mode='Prepare',
 [string]$FixtureRoot,[string]$OutputFile,[string]$Before,[string]$After,[switch]$RequireReboot)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$source=(& git -C $repo rev-parse HEAD).Trim()
$probe=Join-Path $repo 'tools/SmartPerformanceDoctor.WO24Probe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.WO24Probe.exe'
if($Mode -eq 'Prepare'){
 [ordered]@{Status='PREPARED';Source=$source;Vm='VM_UNAVAILABLE';ExecutionGranted=$false;
  ExistingPreparation=@('wo21-prepare-vm.ps1','wo21-test-signing.ps1','SmartPerformanceDoctor.GuiProbe');
  Required=@('Hypervisor-observed checkpoint','Independent administrator and standard-user SID','Disposable guest trust store','Protected synthetic installation','Interactive UAC','Recorded reboot boot identity');
  Supported=@('Read-only history capture','Boot identity collection','Content comparison','No automatic reboot or file restoration')}|ConvertTo-Json -Depth 5
 return
}
if(!(Test-Path -LiteralPath $probe)){throw 'WO24_BUILD_PROBE_FIRST'}
if(!$OutputFile -or (Test-Path -LiteralPath $OutputFile)){throw 'NEW_OUTPUT_FILE_REQUIRED'}
if($Mode -eq 'Capture'){
 if(![IO.Path]::IsPathFullyQualified($FixtureRoot)){throw 'ABSOLUTE_SYNTHETIC_ROOT_REQUIRED'}
 $uuid=(Get-CimInstance Win32_ComputerSystemProduct).UUID
 $boot=(Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime().ToString('O')
 $sha=[Security.Cryptography.SHA256]::Create()
 try{$env:WO24_MACHINE=[Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($uuid)))}finally{$sha.Dispose()}
 $env:WO24_BOOT=$boot;$env:WO24_SOURCE=$source
 $raw=& $probe capture $FixtureRoot;$code=$LASTEXITCODE
 if($code -ne 0){throw "WO24_CAPTURE_FAILED:$code"}
 $result=$raw|ConvertFrom-Json
 $result.Observation|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $OutputFile
 $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
 $sid=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($identity.User.Value)))
 @{Source=$source;Command='WO24Probe capture <SYNTHETIC_ROOT>';ExitCode=$code;SidHash=$sid;Administrator=([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator);BinaryHash=(Get-FileHash $probe).Hash;Result=$result}|ConvertTo-Json -Depth 12|Set-Content -LiteralPath ($OutputFile+'.details.json')
}else{
 $raw=& $probe compare $Before $After $RequireReboot.IsPresent.ToString();$code=$LASTEXITCODE
 @{Source=$source;Command='WO24Probe compare <BEFORE> <AFTER> <REQUIRE_REBOOT>';ExitCode=$code;Output=($raw|ConvertFrom-Json);Scope='Evidence comparison only; not independent attestation'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $OutputFile
 if($code -ne 0){throw "WO24_COMPARISON_REVIEW:$code"}
}
