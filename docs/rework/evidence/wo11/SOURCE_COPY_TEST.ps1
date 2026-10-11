$ErrorActionPreference='Stop'
Set-Location ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../')))
$fixture='artifacts/wo11/gate-fixture'
New-Item -ItemType Directory -Force $fixture | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'Fixture.Program.cs.txt') (Join-Path $fixture 'Program.cs')
Copy-Item (Join-Path $PSScriptRoot 'Fixture.gate-fixture.csproj.txt') (Join-Path $fixture 'gate-fixture.csproj')
foreach($phase in @('BASELINE','MODIFIED','ROLLBACK')) {
 if($phase -eq 'BASELINE') { Copy-Item (Join-Path $PSScriptRoot 'ORIGINAL_FILE.cs') (Join-Path $fixture 'Gate.cs'); $expected='True' }
 if($phase -eq 'MODIFIED') { Copy-Item (Join-Path $PSScriptRoot 'MODIFIED_FILE.cs') (Join-Path $fixture 'Gate.cs'); $expected='False' }
 if($phase -eq 'ROLLBACK') {
  Copy-Item (Join-Path $PSScriptRoot 'MODIFIED_FILE.cs') artifacts/wo11/rollback-copy.cs
  & (Join-Path $PSScriptRoot 'ROLLBACK.ps1') -Target artifacts/wo11/rollback-copy.cs
  Copy-Item artifacts/wo11/rollback-copy.cs (Join-Path $fixture 'Gate.cs'); $expected='True'
 }
 dotnet build "$fixture/gate-fixture.csproj" -t:Rebuild -m:1 -v:minimal
 if($LASTEXITCODE -ne 0){throw "Fixture build failed: $phase"}
 dotnet run --project $fixture --no-build -- $expected
 if($LASTEXITCODE -ne 0){throw "Fixture behavior failed: $phase"}
}
