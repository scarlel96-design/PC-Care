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
$msiBytes = if ([string]::IsNullOrWhiteSpace($MsiPath) -or -not (Test-Path -LiteralPath $MsiPath)) {
    [byte[]]::new(0)
} else {
    [byte[]][IO.File]::ReadAllBytes($MsiPath)
}

$magic = [Text.Encoding]::ASCII.GetBytes("SPDPKG1`0")
$layoutLen = [BitConverter]::GetBytes([int64]$layoutBytes.Length)
$msiLen = [BitConverter]::GetBytes([int64]$msiBytes.Length)

$setupBytes = [IO.File]::ReadAllBytes($SetupExe)
$combined = New-Object byte[] ($setupBytes.Length + $layoutBytes.Length + $msiBytes.Length + $magic.Length + 16)
[Array]::Copy($setupBytes, 0, $combined, 0, $setupBytes.Length)
$offset = $setupBytes.Length
[Array]::Copy($layoutBytes, 0, $combined, $offset, $layoutBytes.Length)
$offset += $layoutBytes.Length
if ($msiBytes.Length -gt 0) {
    [Array]::Copy($msiBytes, 0, $combined, $offset, $msiBytes.Length)
    $offset += $msiBytes.Length
}
[Array]::Copy($magic, 0, $combined, $offset, $magic.Length)
$offset += $magic.Length
[Array]::Copy($layoutLen, 0, $combined, $offset, 8)
$offset += 8
[Array]::Copy($msiLen, 0, $combined, $offset, 8)

$boundary = $setupBytes.Length
if ($layoutBytes.Length -gt 0) {
    if ($boundary + 3 -ge $combined.Length) {
        throw "Installer payload boundary is invalid."
    }
    if ($combined[$boundary] -ne 0x50 -or $combined[$boundary + 1] -ne 0x4B -or $combined[$boundary + 2] -ne 0x03 -or $combined[$boundary + 3] -ne 0x04) {
        throw "Layout zip must start at PK`x03`x04 immediately after setup host (offset $boundary)."
    }
}

[IO.File]::WriteAllBytes($OutputExe, $combined)
$sizeMb = [math]::Round($combined.Length / 1MB, 1)
Write-Host "[OK] Single installer: $OutputExe ($sizeMb MB)" -ForegroundColor Green