[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OperationTrxPath,

    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

# This reviewer only uses PowerShell's XML/text/hash facilities. It does not load
# PCCare assemblies or call product APIs. Its result is an internal independent-
# process review, not an external third-party attestation.
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path -Parent $OperationTrxPath) 'independent-process-security-review.json'
}
if (-not (Test-Path -LiteralPath $OperationTrxPath -PathType Leaf)) { throw "TRX file is missing: $OperationTrxPath" }

function Read-Text([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Review source is missing: $path" }
    return Get-Content -LiteralPath $path -Raw
}
function New-Rule([string]$id, [string]$description, [bool]$passed) {
    [ordered]@{ id = $id; description = $description; passed = $passed }
}

$purge = Read-Text 'src\SmartPerformanceDoctor.App\Services\Commercial\WinPeStoragePurgeExecutor.cs'
$purgeEvidence = Read-Text 'src\SmartPerformanceDoctor.App\Services\Commercial\StoragePurgeExecutionEvidence.cs'
$deleteService = Read-Text 'src\SmartPerformanceDoctor.App\Services\Commercial\ProfessionalSecureDeleteService.cs'
$vaultCrypto = Read-Text 'src\SmartPerformanceDoctor.SecurityLab\src\VaultV4\LabVaultCrypto.cs'

[xml]$trx = Get-Content -LiteralPath $OperationTrxPath -Raw
$results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
$passedTests = @($results | Where-Object { $_.outcome -eq 'Passed' })
$failedTests = @($results | Where-Object { $_.outcome -ne 'Passed' })

$rules = @(
    (New-Rule 'PURGE-WINPE' 'WinPE and OS gate is present before dispatch.' ($purge -match 'OperatingSystem\.IsWindows\(\).*_isWinPe\(\).*request\.Plan\.IsWinPe')),
    (New-Rule 'PURGE-BOOT-SYSTEM' 'Boot/system disks are rejected before dispatch.' ($purge -match 'IsBootDisk.*IsSystemDisk')),
    (New-Rule 'PURGE-CONFIRM' 'Exact disk-bound confirmation is required.' ($purge -match 'RequiredConfirmation\(request\.Method, request\.Plan\.Device\.DiskNumber\)')),
    (New-Rule 'PURGE-UNCERTAIN' 'Native transport errors retain submission uncertainty.' ($purge -match 'PurgeDispatchState\.SubmissionUncertain')),
    (New-Rule 'PURGE-EVIDENCE' 'Execution evidence carries explicit dispatch state.' ($purgeEvidence -match 'DispatchState = result\.DispatchState\.ToString\(\)')),
    (New-Rule 'DELETE-TOCTOU' 'File/folder target fingerprint is rechecked before delete.' ($deleteService -match 'CaptureTargetFingerprint\(target\.Path\)')),
    (New-Rule 'DELETE-REPARSE' 'Directory snapshots reject reparse points.' ($deleteService -match '재분석 지점')),
    (New-Rule 'VAULT-KDF' 'Vault crypto exposes Argon2id derivation.' ($vaultCrypto -match 'DeriveArgon2id')),
    (New-Rule 'VAULT-AEAD' 'Vault crypto includes XChaCha20-Poly1305 suite.' ($vaultCrypto -match 'XChaCha20Poly1305')),
    (New-Rule 'OPERATION-TRX' 'All supplied actual filesystem operation tests passed.' ($passedTests.Count -eq 3 -and $failedTests.Count -eq 0))
)

$sources = @(
    'src\SmartPerformanceDoctor.App\Services\Commercial\WinPeStoragePurgeExecutor.cs',
    'src\SmartPerformanceDoctor.App\Services\Commercial\StoragePurgeExecutionEvidence.cs',
    'src\SmartPerformanceDoctor.App\Services\Commercial\ProfessionalSecureDeleteService.cs',
    'src\SmartPerformanceDoctor.SecurityLab\src\VaultV4\LabVaultCrypto.cs'
) | ForEach-Object {
    $path = Join-Path $repoRoot $_
    $hash = Get-FileHash -LiteralPath $path -Algorithm SHA256
    [ordered]@{ path = $_; sha256 = $hash.Hash.ToLowerInvariant() }
}
$trxHash = Get-FileHash -LiteralPath $OperationTrxPath -Algorithm SHA256
$payload = [ordered]@{
    schema = 'pc-care.independent-process-security-review.v1'
    reviewedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    reviewer = 'PowerShell text/XML/hash verifier; no PCCare assemblies or APIs loaded'
    operationTrx = [ordered]@{ path = (Resolve-Path $OperationTrxPath).Path; sha256 = $trxHash.Hash.ToLowerInvariant(); passed = $passedTests.Count; failedOrOther = $failedTests.Count }
    sourceHashes = @($sources)
    rules = @($rules)
    passed = @($rules | Where-Object passed).Count
    failed = @($rules | Where-Object { -not $_.passed }).Count
    limitations = @(
        'This is an internal independent-process reproduction and static review.',
        'It is not a review by an external person, organization, or certification body.',
        'It does not prove physical-media sanitization or forensic irrecoverability.'
    )
}
$payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Host "Independent-process review: $OutputPath"
if ($payload.failed -ne 0) { exit 1 }
