param(
    [Parameter(Mandatory=$true)][string]$ExpectedTestSid,
    [Parameter(Mandatory=$true)][string]$OriginalUserSid,
    [string]$SnapshotId,
    [string]$AppPath,
    [string]$ProbePath,
    [switch]$DisposableVmConfirmed,
    [switch]$Launch
)
$ErrorActionPreference = 'Stop'
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
if ($sid -ne $ExpectedTestSid -or $sid -eq $OriginalUserSid) {
    '{"status":"BLOCKED","guiExecuted":false,"reason":"Independent test SID required; no profile changes made"}'
    exit 4
}
if (!$DisposableVmConfirmed -or [string]::IsNullOrWhiteSpace($SnapshotId)) {
    '{"status":"BLOCKED","guiExecuted":false,"reason":"Disposable VM and snapshot evidence required"}'
    exit 5
}
$profile = [Environment]::GetFolderPath('UserProfile')
$local = [Environment]::GetFolderPath('LocalApplicationData')
if (!$local.StartsWith($profile.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Local AppData must belong to the independent profile'
}
$testRoot = Join-Path $local 'PCCareWO16Synthetic'
for ($node=$testRoot; $node; $node=Split-Path $node -Parent) {
    if ((Test-Path -LiteralPath $node) -and ((Get-Item -LiteralPath $node).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Profile reparse point rejected'
    }
}
if (!$Launch) {
    '{"status":"NOT TESTED","guiExecuted":false,"reason":"Environment prerequisites checked; explicit Launch required"}'
    exit 0
}
if (!(Test-Path -LiteralPath $AppPath -PathType Leaf) -or !(Test-Path -LiteralPath $ProbePath -PathType Leaf)) {
    throw 'Built application and GuiProbe required'
}
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
foreach ($name in @('vault','temp','logs','cache')) {
    New-Item -ItemType Directory -Path (Join-Path $testRoot $name) -Force | Out-Null
}
Set-Content -LiteralPath (Join-Path $testRoot 'fixture.marker') -Value 'WO15_SYNTHETIC_ONLY' -NoNewline
$acl = Get-Acl -LiteralPath $testRoot
[pscustomobject]@{ status='PARTIAL'; guiExecuted=$false; snapshot=$SnapshotId; dedicatedSidVerified=$true; owner=$acl.Owner; sddl=$acl.Sddl } | ConvertTo-Json
$env:SPD_TEST_VAULT_ROOT = Join-Path $testRoot 'vault'
$env:TEMP = Join-Path $testRoot 'temp'
$env:TMP = $env:TEMP
# App settings remain in this independent account's AppData.
# The logs and cache directories are preparation assets, not application redirects.
$app = Start-Process -FilePath $AppPath -PassThru -WindowStyle Normal
Write-Output 'Open the vault page. Use only synthetic files in this test profile.'
Read-Host 'Press Enter after the vault page is visible' | Out-Null
& $ProbePath --inspect-vault-page $app.Id
exit $LASTEXITCODE
