param([Parameter(Mandatory)][string]$FixtureDirectory)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')) + [IO.Path]::DirectorySeparatorChar
$fixture = [IO.Path]::GetFullPath($FixtureDirectory)
if (!$fixture.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'SOURCE-ROLLBACK-LOCATION' }
$marker = Join-Path $fixture '.wo06-source-copy'
if (!(Test-Path -LiteralPath $marker) -or [IO.File]::ReadAllText($marker) -ne 'WO06-INDEPENDENT-SOURCE-COPY') {
    throw 'SOURCE-ROLLBACK-MARKER'
}
if ((Get-Item -LiteralPath $fixture).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'SOURCE-ROLLBACK-REPARSE' }
$source = Join-Path $PSScriptRoot 'UpdateTransaction.original.cs'
$expected = '63A4AD13AB5A4B105C4B438DD4DC25E3F1449AE918EBA005946103DC77871C94'
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $expected) { throw 'SOURCE-ROLLBACK-BASE-HASH' }
$target = Join-Path $fixture 'UpdateTransaction.cs'
if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'SOURCE-ROLLBACK-TARGET-REPARSE' }
Copy-Item -LiteralPath $source -Destination $target -Force
if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expected) { throw 'SOURCE-ROLLBACK-RESULT-HASH' }
'SOURCE_ROLLBACK=PASS; RESTORED_SHA256=' + $expected
