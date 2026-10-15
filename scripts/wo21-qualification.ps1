param(
 [ValidateSet('Build','Test','All','ValidateOnly')][string]$Mode='All',
 [Parameter(Mandatory=$true)][string]$FixtureRoot,
 [string]$OutputDirectory='artifacts/wo21-qualified',
 [string]$Filter=''
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if(![IO.Path]::IsPathFullyQualified($FixtureRoot)){throw 'WO21_ROOT_ABSOLUTE_REQUIRED'}
$fixture=[IO.Path]::GetFullPath($FixtureRoot).TrimEnd('\')
foreach($protected in @($repo,$env:windir,$env:ProgramFiles,${env:ProgramFiles(x86)},$env:ProgramData)){
 if($protected -and ($fixture.Equals($protected,[StringComparison]::OrdinalIgnoreCase) -or $fixture.StartsWith($protected.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase))){throw 'WO21_ROOT_PROTECTED'}
}
if(!(Split-Path $fixture -Leaf).StartsWith('wo21-',[StringComparison]::Ordinal)){throw 'WO21_ROOT_PREFIX_REQUIRED'}
for($ancestor=$fixture;$ancestor;$ancestor=Split-Path $ancestor -Parent){
 if((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'WO21_ROOT_REPARSE'}
}
$output=[IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
if(!$output.StartsWith((Join-Path $repo 'artifacts')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'WO21_OUTPUT_ARTIFACTS_REQUIRED'}
if($Mode -eq 'ValidateOnly'){'WO21_ROOT_VALIDATED';return}
New-Item -ItemType Directory -Force $fixture,$output | Out-Null
$env:RUNNER_TEMP=$fixture;$env:VSTEST_CONNECTION_TIMEOUT='300'
$mutex=[Threading.Mutex]::new($false,'Local\PC-Care-WO21-Build-Test')
if(!$mutex.WaitOne(0)){ $mutex.Dispose();throw 'WO21_BUILD_OR_TEST_ALREADY_RUNNING' }
function Fingerprint {
 $names=& git -C $repo ls-files --cached --others --exclude-standard -- src tests tools scripts .github Directory.Build.props Directory.Packages.props global.json NuGet.config
 $lines=foreach($name in ($names|Sort-Object -Unique)){
  $path=Join-Path $repo $name
  if(Test-Path -LiteralPath $path -PathType Leaf){$name+' '+(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
 }
 $sha=[Security.Cryptography.SHA256]::Create()
 try{[Convert]::ToHexString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))))}finally{$sha.Dispose()}
}
function Binaries {
 $base=Join-Path $repo 'tests/SmartPerformanceDoctor.Tests/bin/x64/Release/net10.0-windows10.0.26100.0'
 if(!(Test-Path -LiteralPath (Join-Path $base 'SmartPerformanceDoctor.Tests.dll'))){throw 'WO21_TEST_BINARY_MISSING'}
 @(Get-ChildItem -LiteralPath $base -File -Recurse | Where-Object {$_.Extension -in '.dll','.exe','.json'} | Sort-Object FullName | ForEach-Object {[ordered]@{Name=$_.FullName.Substring($base.Length+1);Hash=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
}
function Run([string]$Name,[string[]]$Arguments){
 $start=[DateTimeOffset]::UtcNow
 & dotnet @Arguments *> (Join-Path $output ($Name+'.txt'))
 $code=$LASTEXITCODE
 @{Command='dotnet '+($Arguments -join ' ');Input=$Filter;Start=$start;End=[DateTimeOffset]::UtcNow;ExitCode=$code;Source=$head;InputDigest=$digest} | ConvertTo-Json | Set-Content (Join-Path $output ($Name+'-command.json'))
 if($code -ne 0){Get-Content (Join-Path $output ($Name+'.txt')) -Tail 20;throw "WO21_COMMAND_FAILED:$Name"}
}
try{
 Push-Location $repo
 $head=(& git rev-parse HEAD).Trim();$digest=Fingerprint
 $receiptPath=Join-Path $output 'build-receipt.json'
 if($Mode -in 'Build','All'){
  Run 'build' @('build','tests/SmartPerformanceDoctor.Tests','-c','Release','-p:Platform=x64','--no-restore','-m:1','-v:minimal')
  if((Fingerprint) -ne $digest){throw 'WO21_SOURCE_CHANGED_DURING_BUILD'}
  @{Source=$head;InputDigest=$digest;BinaryPath='tests/SmartPerformanceDoctor.Tests/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.Tests.dll';Binaries=@(Binaries)} | ConvertTo-Json -Depth 5 | Set-Content $receiptPath
 }
 if($Mode -in 'Test','All'){
  $receipt=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
  if($receipt.Source -ne $head -or $receipt.InputDigest -ne $digest){throw 'WO21_STALE_BUILD_SOURCE'}
  $actual=@(Binaries);if(($actual|ConvertTo-Json -Depth 4 -Compress) -ne ($receipt.Binaries|ForEach-Object {[ordered]@{Name=$_.Name;Hash=$_.Hash}}|ConvertTo-Json -Depth 4 -Compress)){throw 'WO21_BINARY_CHANGED'}
  $trxName='wo21-qualified-'+[guid]::NewGuid().ToString('N')+'.trx'
  $arguments=@('test','tests/SmartPerformanceDoctor.Tests','-c','Release','-p:Platform=x64','--no-build','--no-restore','-m:1','--logger',('trx;LogFileName='+$trxName),'-v:minimal')
  if($Filter){$arguments+=@('--filter',$Filter)}
  Run 'test' $arguments
  $trxPath=Join-Path $repo ('tests/SmartPerformanceDoctor.Tests/TestResults/'+$trxName)
  if(!(Test-Path -LiteralPath $trxPath)){throw 'WO22_FRESH_TRX_MISSING'}
  Copy-Item -LiteralPath $trxPath -Destination (Join-Path $output 'test.trx')
  $testReceipt=Get-Content (Join-Path $output 'test-command.json') -Raw|ConvertFrom-Json
  $testReceipt|Add-Member -NotePropertyName TrxFile -NotePropertyValue $trxName
  $testReceipt|ConvertTo-Json|Set-Content (Join-Path $output 'test-command.json')
  if((Fingerprint) -ne $digest){throw 'WO21_SOURCE_CHANGED_DURING_TEST'}
  Get-Content (Join-Path $output 'test.txt') -Tail 4
 }
 'WO21_QUALIFICATION=PASS'
}finally{Pop-Location;$mutex.ReleaseMutex();$mutex.Dispose()}
