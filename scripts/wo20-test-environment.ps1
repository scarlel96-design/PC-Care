param([string]$OutputDirectory='artifacts/wo20-tests',[string]$FixtureRoot)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$root=[IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
if(!$root.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'WO20_TEST_ROOT_OUTSIDE_WORKSPACE'}
if($root.StartsWith((Join-Path $repo 'src'),[StringComparison]::OrdinalIgnoreCase)-or $root.StartsWith((Join-Path $repo 'tests'),[StringComparison]::OrdinalIgnoreCase)){throw 'WO20_TEST_ROOT_PROTECTED'}
if([string]::IsNullOrWhiteSpace($FixtureRoot)){throw 'WO20_FIXTURE_ROOT_REQUIRED'}
if(![IO.Path]::IsPathFullyQualified($FixtureRoot)){throw 'WO20_FIXTURE_ROOT_ABSOLUTE_REQUIRED'}
$fixture=[IO.Path]::GetFullPath($FixtureRoot)
if($fixture.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'WO20_FIXTURE_ROOT_INSIDE_PRODUCT_TREE'}
foreach($blocked in @([Environment]::GetFolderPath('Windows'),[Environment]::GetFolderPath('ProgramFiles'))){
 if($blocked -and $fixture.StartsWith($blocked,[StringComparison]::OrdinalIgnoreCase)){throw 'WO20_FIXTURE_ROOT_PROTECTED'}
}
if([IO.Path]::GetFileName($fixture) -notlike 'wo20-*'){throw 'WO20_FIXTURE_ROOT_NAME_REQUIRED'}
$env:RUNNER_TEMP=$fixture
$cursor=$env:RUNNER_TEMP
while($cursor){
 if((Test-Path -LiteralPath $cursor)-and ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'WO20_TEST_ROOT_REPARSE'}
 $cursor=[IO.Path]::GetDirectoryName($cursor)
}
New-Item -ItemType Directory -Force $env:RUNNER_TEMP | Out-Null
$env:VSTEST_CONNECTION_TIMEOUT='300'
Write-Output 'WO20_TEST_ENVIRONMENT=ISOLATED; RUNNER_TEMP=EXPLICIT'
