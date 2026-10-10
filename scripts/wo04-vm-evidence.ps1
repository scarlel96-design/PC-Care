param(
    [Parameter(Mandatory=$true)][string]$InstallRoot,
    [Parameter(Mandatory=$true)][string]$EvidenceDirectory,
    [Parameter(Mandatory=$true)][string]$SnapshotId,
    [Parameter(Mandatory=$true)][ValidateSet('before','after')][string]$Phase
)
$ErrorActionPreference = 'Stop'
$install = (Resolve-Path -LiteralPath $InstallRoot).Path
$evidence = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
if (!(Test-Path -LiteralPath (Join-Path $evidence '.wo04-disposable-vm'))) {
    throw 'Missing disposable VM marker; run only inside the prepared test VM.'
}
if (!$SnapshotId.Trim()) { throw 'Snapshot evidence is required.' }
if (Get-ChildItem -LiteralPath $install -Recurse -Force | Where-Object {
    $_.Attributes -band [IO.FileAttributes]::ReparsePoint
}) { throw 'Reparse path found; stop and preserve evidence.' }
$files = @(Get-ChildItem -LiteralPath $install -File -Recurse -Force | ForEach-Object {
    [pscustomobject]@{ Path=$_.FullName.Substring($install.Length); Length=$_.Length;
        Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$state = [pscustomobject]@{ Phase=$Phase; SnapshotId=$SnapshotId; Utc=[DateTime]::UtcNow.ToString('o');
    InstallRoot=$install; WindowsVersion=[Environment]::OSVersion.Version.ToString();
    Elevated=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator); Files=$files }
$state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence "$Phase-files.json") -Encoding utf8
$protected = Join-Path $env:ProgramData 'PC-Care/UpdateTransactions'
if (Test-Path -LiteralPath $protected) {
    Get-Acl -LiteralPath $protected | Format-List Owner,Sddl,AccessToString |
        Out-File -LiteralPath (Join-Path $evidence "$Phase-acl.txt") -Encoding utf8
}
Write-Output "CAPTURED phase=$Phase files=$($files.Count) snapshot=$SnapshotId"
# This collector does not install, delete, reboot, approve UAC, or certify test results.
