[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('SecureVault', 'SecureDelete', 'DevicePurge')]
    [string]$Feature,

    [Parameter(Mandatory)]
    [string]$Scenario,

    [Parameter(Mandatory)]
    [ValidateSet('ReadOnly', 'VaultSeal', 'FileClear', 'PurgeAttempted', 'PurgeCompleted')]
    [string]$CommandDisposition,

    [Parameter(Mandatory)]
    [string]$TargetFingerprint,

    [Parameter(Mandatory)]
    [string]$OperatorIdentifier,

    [Parameter(Mandatory)]
    [string[]]$ArtifactPath,

    [string]$OutputRoot = (Join-Path $PSScriptRoot "..\..\artifacts\security-operation-evidence")
)

$ErrorActionPreference = 'Stop'

# This is an evidence packer, not an execution engine. It never invokes a delete,
# sanitize, format, or storage-management command. It cannot promote the evidence
# to independent verification; a separately identified reviewer must do that.
function Get-Utf8Sha256([string]$value) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($value)
    try { return ([Security.Cryptography.SHA256]::HashData($bytes) | ForEach-Object ToString x2) -join '' }
    finally { [Security.Cryptography.CryptographicOperations]::ZeroMemory($bytes) }
}

if ([string]::IsNullOrWhiteSpace($TargetFingerprint)) { throw 'TargetFingerprint must not be empty.' }
if ([string]::IsNullOrWhiteSpace($OperatorIdentifier)) { throw 'OperatorIdentifier must not be empty.' }
if ($CommandDisposition -ne 'ReadOnly' -and $ArtifactPath.Count -lt 2) {
    throw 'A state-changing scenario requires at least two artifacts (for example, pre/post status or an app report plus raw device status).'
}

$artifactRecords = foreach ($path in $ArtifactPath) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Evidence artifact is missing: $path" }
    $item = Get-Item -LiteralPath $path
    $hash = Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256
    [ordered]@{ path = $item.FullName; bytes = $item.Length; sha256 = $hash.Hash.ToLowerInvariant() }
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$id = "$($Feature.ToLowerInvariant())-operation-$stamp-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$directory = Join-Path $OutputRoot $id
New-Item -ItemType Directory -Path $directory -Force | Out-Null

$hostIdentity = "$( $env:COMPUTERNAME )|$([Environment]::OSVersion.VersionString)|$([Environment]::Is64BitOperatingSystem)"
$payload = [ordered]@{
    schema = 'pc-care.security-operation-evidence.v1'
    id = $id
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    feature = $Feature
    scenario = $Scenario
    commandDisposition = $CommandDisposition
    targetFingerprint = $TargetFingerprint
    operatorIdentifierSha256 = Get-Utf8Sha256 $OperatorIdentifier
    hostFingerprintSha256 = Get-Utf8Sha256 $hostIdentity
    artifacts = @($artifactRecords)
    assessment = [ordered]@{
        actualOperation = 'unreviewed: evidence captured, not automatically accepted as a successful operation'
        independentVerification = 'not-evaluated: this bundle has no external witness or reviewer attestation'
    }
    limitations = @(
        'This packer did not dispatch a delete, sanitize, format, or storage command.',
        'Artifact hashes establish the identity of collected files, not the truth of their contents.',
        'A controller completion or application report is not a guarantee of forensic irrecoverability.'
    )
}

$outputPath = Join-Path $directory 'operation-evidence.json'
$payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputPath -Encoding utf8
Write-Host "Operation evidence bundle: $outputPath"
