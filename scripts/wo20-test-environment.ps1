param([string]$OutputDirectory='artifacts/wo20-tests')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$root=[IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
if(!$root.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'WO20_TEST_ROOT_OUTSIDE_WORKSPACE'}
if($root.StartsWith((Join-Path $repo 'src'),[StringComparison]::OrdinalIgnoreCase)-or $root.StartsWith((Join-Path $repo 'tests'),[StringComparison]::OrdinalIgnoreCase)){throw 'WO20_TEST_ROOT_PROTECTED'}
$env:RUNNER_TEMP=Join-Path $root 'fixtures'
$cursor=$env:RUNNER_TEMP
while($cursor){
 if((Test-Path -LiteralPath $cursor)-and ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'WO20_TEST_ROOT_REPARSE'}
 $cursor=[IO.Path]::GetDirectoryName($cursor)
}
New-Item -ItemType Directory -Force $env:RUNNER_TEMP | Out-Null
$env:VSTEST_CONNECTION_TIMEOUT='300'
Write-Output 'WO20_TEST_ENVIRONMENT=ISOLATED; RUNNER_TEMP=EXPLICIT'
