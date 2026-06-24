param(
    [string]$SetupExe,
    [string]$LayoutZip,
    [string]$MsiPath,
    [string]$OutputExe
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SetupExe)) { throw "Setup EXE not found: $SetupExe" }
if (-not (Test-Path $LayoutZip)) { throw "Layout zip not found: $LayoutZip" }

$layoutBytes = [IO.File]::ReadAllBytes($LayoutZip)
$msiBytes = if ($MsiPath -and (Test-Path $MsiPath)) { [IO.File]::ReadAllBytes($MsiPath) } else { [byte[]]::Empty }

$magic = [Text.Encoding]::ASCII.GetBytes("SPDPKG1`0")
$layoutLen = [BitConverter]::GetBytes([int64]$layoutBytes.Length)
$msiLen = [BitConverter]::GetBytes([int64]$msiBytes.Length)

$setupBytes = [IO.File]::ReadAllBytes($SetupExe)
$combined = New-Object byte[] ($setupBytes.Length + $layoutBytes.Length + $msiBytes.Length + $magic.Length + 16)
[Array]::Copy($setupBytes, 0, $combined, 0, $setupBytes.Length)
$offset = $setupBytes.Length
[Array]::Copy($layoutBytes, 0, $combined, $offset, $layoutBytes.Length)
$offset += $layoutBytes.Length
[Array]::Copy($msiBytes, 0, $combined, $offset, $msiBytes.Length)
$offset += $msiBytes.Length
[Array]::Copy($magic, 0, $combined, $offset, $magic.Length)
$offset += $magic.Length
[Array]::Copy($layoutLen, 0, $combined, $offset, 8)
$offset += 8
[Array]::Copy($msiLen, 0, $combined, $offset, 8)

[IO.File]::WriteAllBytes($OutputExe, $combined)
$sizeMb = [math]::Round($combined.Length / 1MB, 1)
Write-Host "[OK] Single installer: $OutputExe ($sizeMb MB)" -ForegroundColor Green