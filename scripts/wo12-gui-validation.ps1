param(
    [string]$ExpectedTestSid,
    [string]$OriginalUserSid,
    [string]$AppPath,
    [string]$ProbePath,
    [switch]$Launch
)
$ErrorActionPreference = 'Stop'
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
if (!$ExpectedTestSid -or !$OriginalUserSid -or $sid -ne $ExpectedTestSid -or $sid -eq $OriginalUserSid) {
    '{"status":"BLOCKED","guiExecuted":false,"reason":"Dedicated test account required"}'
    exit 4
}
if (!$Launch) {
    '{"status":"NOT TESTED","guiExecuted":false,"reason":"Explicit Launch required"}'
    exit 0
}
if (!(Test-Path -LiteralPath $AppPath -PathType Leaf) -or !(Test-Path -LiteralPath $ProbePath -PathType Leaf)) {
    throw 'Built application and GuiProbe required'
}
$profile = [Environment]::GetFolderPath('UserProfile')
$local = [Environment]::GetFolderPath('LocalApplicationData')
if (!$local.StartsWith($profile + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Independent profile required' }
$testRoot = Join-Path $local 'PCCareWO12Synthetic'
for ($node=$testRoot; $node; $node=Split-Path $node -Parent) {
    if ((Test-Path -LiteralPath $node) -and ((Get-Item -LiteralPath $node).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Reparse profile rejected'
    }
}
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$env:SPD_TEST_VAULT_ROOT = Join-Path $testRoot 'vault'
$app = Start-Process -FilePath $AppPath -PassThru
Write-Output 'Open the vault page in this dedicated test session.'
Read-Host 'Press Enter after the vault page is visible' | Out-Null
& $ProbePath --inspect-vault-page $app.Id
exit $LASTEXITCODE
